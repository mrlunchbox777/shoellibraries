using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using StandardDot.Abstract.Caching;
using StandardDot.Abstract.CoreServices;

namespace StandardDot.TestClasses.AbstractImplementations
{
    public class TestMemoryCachingService : ICachingService
    {
        /// <param name="defaultCacheLifespan">How long items should be cached by default</param>
        /// <param name="cache">The cache to use, default is a thread safe dictionary</param>
        public TestMemoryCachingService(TimeSpan defaultCacheLifespan, IDictionary<string, ICachedObject<object>> cache = null)
        {
            DefaultCacheLifespan = defaultCacheLifespan;
            Store = cache ?? new ConcurrentDictionary<string, ICachedObject<object>>();
        }

        /// <param name="defaultCacheLifespan">How long items should be cached by default</param>
        /// <param name="useStaticCache">If this instance should use a static cache (thread safe)</param>
        public TestMemoryCachingService(TimeSpan defaultCacheLifespan, bool useStaticCache)
        {
            DefaultCacheLifespan = defaultCacheLifespan;
            Store = useStaticCache ? _store : new ConcurrentDictionary<string, ICachedObject<object>>();
        }

        private static IDictionary<string, ICachedObject<object>> _store = new ConcurrentDictionary<string, ICachedObject<object>>();

        protected virtual IDictionary<string, ICachedObject<object>> Store { get; }

        /// <summary>
        /// Wraps an object for caching
        /// </summary>
        /// <typeparam name="T">The configuration type</typeparam>
        /// <param name="value">The object to wrap</param>
        /// <param name="cachedTime">When the object was cached, default UTC now</param>
        /// <param name="expireTime">When the object should expire, default UTC now + DefaultCacheLifespan</param>
        /// <returns>The wrapped object</returns>
        protected virtual ICachedObject<T> CreateCachedObject<T>(T value, DateTime? cachedTime = null, DateTime? expireTime = null)
        {
            return new TestDefaultCachedObject<T>
            {
                Value = value,
                CachedTime = cachedTime ?? DateTime.UtcNow,
                ExpireTime = expireTime ?? DateTime.UtcNow.Add(DefaultCacheLifespan)
            };
        }

        public virtual TimeSpan DefaultCacheLifespan { get; }

        public virtual ICollection<string> Keys => Store.Keys;

        public virtual ICollection<ICachedObject<object>> Values => Store.Values;

        public int Count => Store.Count;

        public bool IsReadOnly => Store.IsReadOnly;

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
            Store.Add(key, CreateCachedObject((object)value.Value, value.CachedTime, value.ExpireTime));
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
            Cache<T>(key, CreateCachedObject(value, cachedTime, expireTime));
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

            ICachedObject<object> item = Store[key];
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
            return CreateCachedObject<T>(result, item.CachedTime, item.ExpireTime);
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
                return Store.Remove(key);
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