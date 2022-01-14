using System;
using System.Collections.Generic;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace PrijemRemont
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class PrijemRemont : StatefulService
    {
        private RemontService remontService = null;
        private MailRepository mailRepository = null;

        public PrijemRemont(StatefulServiceContext context)
            : base(context)
        {
            remontService = new RemontService(this.StateManager);
            mailRepository = new MailRepository();
        }

        /// <summary>
        /// Optional override to create listeners (e.g., HTTP, Service Remoting, WCF, etc.) for this service replica to handle client or user requests.
        /// </summary>
        /// <remarks>
        /// For more information on service communication, see https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new List<ServiceReplicaListener>
            {
                new ServiceReplicaListener(context =>
                {
                    return new WcfCommunicationListener<IRemont>(
                        context,
                        remontService,
                        WcfUtility.CreateTcpClientBinding(maxMessageSize:1024*1024*1024),
                        this.CreateAddress(context, "PrijemRemontEndpoint"));
                }, "PrijemRemontEndpoint")
            };
        }

        private EndpointAddress CreateAddress(StatefulServiceContext context, string endpointName)
        {
            string host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint(endpointName);
            int port = endpointConfig.Port;
            string scheme = endpointConfig.Protocol.ToString();

            ServiceEventSource.Current.Message("Napravljen PrijemRemont listener!");

            return new EndpointAddress(string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/{3}", scheme, host, port, endpointName));
        }

        /// <summary>
        /// This is the main entry point for your service replica.
        /// This method executes when this replica of your service becomes primary and has write status.
        /// </summary>
        /// <param name="cancellationToken">Canceled when Service Fabric needs to shut down this service replica.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Replace the following sample code with your own logic 
            //       or remove this RunAsync override if it's not needed in your service.

            var myDictionary = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, long>>("myDictionary");

            var uredjajiNaRemontu = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<string> unreadEmails = await mailRepository.GetBodyFromUnreadMails();

                foreach (string email in unreadEmails)
                {
                    try
                    {
                        string[] parts = email.Split('\n');
                        int id = int.Parse(parts[0].Split(':')[1]);
                        double warehouseTime = double.Parse(parts[1].Split(':')[1]);
                        double workHours = double.Parse(parts[2].Split(':')[1]);

                        await remontService.SendToRemont(id, warehouseTime, workHours);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                }

                using (var tx = this.StateManager.CreateTransaction())
                {
                    var result = await myDictionary.TryGetValueAsync(tx, "Counter");

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Current Counter Value: {0}",
                        result.HasValue ? result.Value.ToString() : "Value does not exist.");

                    await myDictionary.AddOrUpdateAsync(tx, "Counter", 0, (key, value) => ++value);

                    // If an exception is thrown before calling CommitAsync, the transaction aborts, all changes are 
                    // discarded, and nothing is saved to the secondary replicas.
                    await tx.CommitAsync();
                }

           

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }
}
