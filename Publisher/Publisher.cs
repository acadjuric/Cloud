using System;
using System.Collections.Generic;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Publisher
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class Publisher : StatelessService
    {
        private PublisherService publisherService = null;
        public Publisher(StatelessServiceContext context)
            : base(context)
        {
            publisherService = new PublisherService();
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new List<ServiceInstanceListener>
            {
                new ServiceInstanceListener(context=> this.CreateListener(context), "PublisherEndpoint"),
            };
        }

        private ICommunicationListener CreateListener(StatelessServiceContext context)
        {
            string host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("PublisherEndpoint");
            int port = endpointConfig.Port;
            string scheme = endpointConfig.Protocol.ToString();

            string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/PublisherEndpoint", scheme, host, port);

            var listener = new WcfCommunicationListener<IPublisher>(
                context,
                publisherService,
                WcfUtility.CreateTcpClientBinding(maxMessageSize: 1024 * 1024 * 1024),
                new EndpointAddress(uri)
                );

            ServiceEventSource.Current.Message("Napravljen PUBLISHER listener!");

            return listener;
        }

        /// <summary>
        /// This is the main entry point for your service instance.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service instance.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
