using IdentityServer4.Events;
using IdentityServer4.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceFabricGateway.IdentityService.Services
{
    public class DefaultEventSink : IEventSink
    {
        /// <summary>
        /// The logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultEventSink"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public DefaultEventSink(ILogger<DefaultEventService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Raises the specified event.
        /// </summary>
        /// <param name="evt">The event.</param>
        /// <exception cref="System.ArgumentNullException">evt</exception>
        public virtual Task PersistAsync(Event evt)
        {
            if (evt.EventType == EventTypes.Success ||
             evt.EventType == EventTypes.Information)
            {
                _logger.LogInformation(new EventId(evt.Id), "{Name} ({Id}), Details: {@details}",
                    evt.Name,
                    evt.Id,
                    evt);
            }
            else
            {
                _logger.LogError(new EventId(evt.Id), "{Name} ({Id}), Details: {@details}",
                    evt.Name,
                    evt.Id,
                    evt);
            }

            return Task.CompletedTask;
        }
    }
}
