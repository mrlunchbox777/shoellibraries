using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;
using StandardDot.Abstract.Caching;
using StandardDot.Abstract.CoreServices;
using StandardDot.Caching.Redis.Abstract;
using StandardDot.Caching.Redis.Dto;
using StandardDot.Caching.Redis.Providers;
using StandardDot.Caching.Redis.Service;
using StandardDot.CoreServices.Extensions;

namespace StandardDot.Caching.Redis
{
	public class RedisCachingService : ICachingService
	{
		/// <param name="defaultCacheLifespan">How long items should be cached by default</param>
		/// <param name="cache">The cache to use, default is a thread safe dictionary</param>
		public RedisCachingService(ICacheProviderSettings settings, ILoggingService loggingService, CacheInfo cacheInfo, bool useStaticProvider = true)
		{
			_settings = settings;
			_cacheInfo = cacheInfo;
			_loggingService = loggingService;
			UseStaticProvider = useStaticProvider;
		}

		protected virtual RedisId GetRedisId(string key)
		{
			return new RedisId
			{
				ServiceType = _settings.RedisServiceImplementationType,
				ObjectIdentifier = key,
				HashSetIdentifier = _settings.PrefixIdentifier
			};
		}

		protected virtual bool UseStaticProvider { get; }

		protected virtual ICacheProviderSettings _settings { get; }

		protected virtual CacheInfo _cacheInfo { get; }

		private ILoggingService _loggingService;

		public ILoggingService LoggingService => _loggingService;

		private static ConcurrentDictionary<Guid, RedisService> _store
			= new ConcurrentDictionary<Guid, RedisService>();

		private RedisService Store
		{
			get
			{
				RedisService provider;
				if (UseStaticProvider && _store.ContainsKey(_settings.CacheProviderSettingsId))
				{
					provider = _store[_settings.CacheProviderSettingsId];
					if (_settings == provider.CacheSettings)
					{
						return provider;
					}
				}
				provider = new RedisService(_settings, LoggingService, (s, l) => new RedisCacheProvider(s, l));
				if (UseStaticProvider)
				{
					_store[_settings.CacheProviderSettingsId] = provider;
				}
				return provider;
			}
		}

		/// <summary>
		/// Wraps an object for caching
		/// </summary>
		/// <typeparam name="T">The configuration type</typeparam>
		/// <param name="key">The key that identifies the object</param>
		/// <param name="value">The object to wrap</param>
		/// <param name="cachedTime">When the object was cached, default UTC now</param>
		/// <param name="expireTime">When the object should expire, default UTC now + DefaultCacheLifespan</param>
		/// <returns>The wrapped object</returns>
		protected virtual RedisCachedObject<T> CreateCachedObject<T>(string key, T value, DateTime? cachedTime = null, DateTime? expireTime = null)
		{
			return new RedisCachedObject<T>(key)
			{
				Value = value,
				CachedTime = cachedTime ?? DateTime.UtcNow,
				ExpireTime = expireTime ?? DateTime.UtcNow.Add(DefaultCacheLifespan),
				Metadata = _cacheInfo
			};
		}

		public virtual TimeSpan DefaultCacheLifespan => _settings.DefaultExpireTimeSpan ?? TimeSpan.FromSeconds(300);

		// needs work
		ICollection<string> IDictionary<string, ICachedObject<object>>.Keys => throw new NotImplementedException();
			// Store.Database.HashKeys("*").Select(x => x.ToString())
			// .Paginate(_settings.DefaultScanPageSize);
			// .ToList();

		// needs work
		ICollection<ICachedObject<object>> IDictionary<string, ICachedObject<object>>.Values
			// => throw new NotImplementedException();
			=> Store.RedisServiceImplementation
			.GetListFromCache<RedisCachedObject<object>>(new List<RedisId> { new RedisId { HashSetIdentifier = "*", ObjectIdentifier = "*" } })
			.Cast<ICachedObject<object>>().ToList();

		// needs work
		public long Count => Store.KeyCount();

		public bool IsReadOnly => false;

		/// <summary>
		/// Gets an object from cache, null if not found
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <returns>The cached wrapped object, default null</returns>
		public ICachedObject<object> this[string key]
		{
			get => Retrieve<object>(key);
			set => Cache<object>(key, value);
		}

		/// <summary>
		/// Caches an object, overwrites it if it is already cached
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <param name="value">The wrapped object to cache</param>
		public virtual void Cache<T>(string key, ICachedObject<T> value)
		{
			if (value == null)
			{
				return;
			}
			if (ContainsKey(key))
			{
				Invalidate(key);
			}
			Store.CacheProvider.SetValue(CreateCachedObject(key, value.Value, value.CachedTime, value.ExpireTime));
		}

		/// <summary>
		/// Caches an object, overwrites it if it is already cached
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <param name="value">The object to cache</param>
		/// <param name="cachedTime">The time the object was cached, default UTC now</param>
		/// <param name="expireTime">When the object should expire, default UTC now + DefaultCacheLifespan</param>
		public void Cache<T>(string key, T value, DateTime? cachedTime = null, DateTime? expireTime = null)
		{
			Cache<T>(key, CreateCachedObject(key, value, cachedTime, expireTime));
		}

		/// <summary>
		/// Gets an object from cache, null if not found
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <returns>The cached wrapped object, default null</returns>
		public ICachedObject<T> Retrieve<T>(string key)
		{
			if (!ContainsKey(key))
			{
				return null;
			}

			ICachedObject<T> item = Store.RedisServiceImplementation.GetFromCache<RedisCachedObject<T>>(GetRedisId(key));
			if (!(item.Value is T))
			{
				return null;
			}
			T result = (T)item.Value;
			if (item.ExpireTime < DateTime.UtcNow)
			{
				Invalidate(key);
				return null;
			}
			return CreateCachedObject<T>(key, result, item.CachedTime, item.ExpireTime);
		}

		/// <summary>
		/// Removes an object from the cache
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <returns>If the object was able to be removed</returns>
		public bool Invalidate(string key)
		{
			if (ContainsKey(key))
			{
				RedisId redisId = GetRedisId(key);
				Store.CacheProvider.DeleteValue(redisId.HashSetIdentifier, redisId.ObjectIdentifier);
				return true;
			}
			return false;
		}

		/// <summary>
		/// Caches an object, overwrites it if it is already cached
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <param name="value">The wrapped object to cache</param>
		public void Add(string key, ICachedObject<object> value)
		{
			Cache<object>(key, value);
		}

		/// <summary>
		/// Checks if an object exists in the cache and is valid
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <returns>If the object was found and valid</returns>
		public bool ContainsKey(string key)
		{
			return Store.ContainsKey(key);
		}

		/// <summary>
		/// Removes an object from the cache
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <returns>If the object was able to be removed</returns>
		public bool Remove(string key)
		{
			return Invalidate(key);
		}

		/// <summary>
		/// Gets an object from cache, null if not found
		/// </summary>
		/// <param name="key">The key that identifies the object</param>
		/// <param name="value">The wrapped object to cache</param>
		/// <returns>If the value was able to be retrieved</returns>
		public bool TryGetValue(string key, out ICachedObject<object> value)
		{
			value = Retrieve<object>(key);
			return value != null;
		}

		/// <summary>
		/// Caches an object, overwrites it if it is already cached
		/// </summary>
		/// <param name="item">(The key that identifies the object, The wrapped object to cache)</param>
		public void Add(KeyValuePair<string, ICachedObject<object>> item)
		{
			Cache(item.Key, item.Value);
		}

		/// <summary>
		/// Clears the cache of all cached items.
		/// </summary>
		public void Clear()
		{
			Store.Clear();
		}

		/// <summary>
		/// Checks if an object exists in the cache and is valid
		/// </summary>
		/// <param name="item">(The key that identifies the object, The wrapped object to cache)</param>
		/// <returns>If the object was found and valid</returns>
		public bool Contains(KeyValuePair<string, ICachedObject<object>> item)
		{
			if (!ContainsKey(item.Key))
			{
				return false;
			}

			ICachedObject<object> value = Store[item.Key];

			if (value.Value == null)
			{
				return false;
			}
			if (value.ExpireTime < DateTime.UtcNow)
			{
				Invalidate(item.Key);
				return false;
			}
			if (value.CachedTime != item.Value.CachedTime)
			{
				return false;
			}
			if (value.ExpireTime != item.Value.ExpireTime)
			{
				return false;
			}
			if (value.Value != item.Value.Value)
			{
				return false;
			}
			return true;
		}

		/// <summary>
		/// Copies the cache to an array
		/// </summary>
		/// <param name="array">The destination of the copy</param>
		/// <param name="arrayIndex">Where to start the copy at in the destination</param>
		public void CopyTo(KeyValuePair<string, ICachedObject<object>>[] array, int arrayIndex)
		{
			Store.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes an object from the cache
		/// </summary>
		/// <param name="item">(The key that identifies the object, The wrapped object to cache)</param>
		/// <returns>If the object was able to be removed</returns>
		public bool Remove(KeyValuePair<string, ICachedObject<object>> item)
		{
			if (!ContainsKey(item.Key))
			{
				return false;
			}

			ICachedObject<object> value = Store[item.Key];

			if (value.Value == null)
			{
				return false;
			}
			if (value.ExpireTime < DateTime.UtcNow)
			{
				Invalidate(item.Key);
				return false;
			}
			if (value.CachedTime != item.Value.CachedTime)
			{
				return false;
			}
			if (value.ExpireTime != item.Value.ExpireTime)
			{
				return false;
			}
			if (value.Value != item.Value.Value)
			{
				return false;
			}
			return Store.Remove(item.Key);
		}

		/// <summary>
		/// Gets the typed enumerator for the cache
		/// </summary>
		/// <returns>The typed enumerator</returns>
		public IEnumerator<KeyValuePair<string, ICachedObject<object>>> GetEnumerator()
		{
			return Store.GetEnumerator();
		}

		/// <summary>
		/// Gets the enumerator for the cache
		/// </summary>
		/// <returns>The enumerator</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return Store.GetEnumerator();
		}
	}
}