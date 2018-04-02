using System;
using shoellibraries.Abstract.Caching;
using shoellibraries.Abstract.Configuration;
using shoellibraries.Abstract.CoreServices;

namespace Configuration
{
    public class DefaultConfigurationCache : ConfigurationCacheBase
    {
        public DefaultConfigurationCache(ICachingService cachingService, ISerializationService serializationService,
            TimeSpan configurationLifeSpan)
            : base(cachingService, serializationService, configurationLifeSpan)
        {
        }
    }
}
