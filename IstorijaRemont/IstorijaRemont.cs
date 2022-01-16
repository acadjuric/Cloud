using System;
using System.Collections.Generic;
using System.Configuration;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using PrijemRemont;

namespace IstorijaRemont
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class IstorijaRemont : StatefulService
    {
        private HistoryRemontService historyRemontService = null;

        private CloudStorageAccount _storageAccount;
        private CloudTable _table;

        public IstorijaRemont(StatefulServiceContext context)
            : base(context)
        {
            string connectionString = ConfigurationManager.AppSettings["CloudConnectionString"];
            _storageAccount = CloudStorageAccount.Parse(connectionString);

            CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
            _table = tableClient.GetTableReference("CloudProjekat");
            _table.CreateIfNotExists();

            historyRemontService = new HistoryRemontService(this.StateManager, _table);
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
                    return new WcfCommunicationListener<IHistoryRemont>(
                        context,
                        historyRemontService,
                        WcfUtility.CreateTcpClientBinding(maxMessageSize: 1024*1024*1024),
                        this.CreateAddres(context,"IstorijaRemontEndpoint")
                    );

                },"IstorijaRemontEndpoint")
            };
        }

        private EndpointAddress CreateAddres(StatefulServiceContext context, string endpointName)
        {
            string host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint(endpointName);
            int port = endpointConfig.Port;
            string scheme = endpointConfig.Protocol.ToString();

            ServiceEventSource.Current.Message("Napravljen ISTORIJA REMONT listener!");

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

            var uredjajiZaIstorju = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontsForHistory");

            var binding = WcfUtility.CreateTcpClientBinding();

            ServicePartitionClient<WcfCommunicationClient<IRemont>> PrijemRemont = new ServicePartitionClient<WcfCommunicationClient<IRemont>>(
                    new WcfCommunicationClientFactory<IRemont>(clientBinding: binding),
                    new Uri("fabric:/Project3/PrijemRemont"),
                    new ServicePartitionKey(0)
                    );

            List<int> keys = new List<int>();

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                //logika za pozivanje PrijemRemont mikroservisa za dobavljanje trenutnih uredjaja na remontu radi provere
                // uslova za upis u istorijsku bazu
                try
                {
                    List<Remont> uredjajiNaRemontu = await PrijemRemont.InvokeWithRetryAsync(client => client.Channel.GetAllRemonts());

                    using (var tx = this.StateManager.CreateTransaction())
                    {
                        await uredjajiZaIstorju.ClearAsync();
                        keys.Clear();

                        foreach (var item in uredjajiNaRemontu)
                        {
                            //Remont je zavrsen ako je proveo bar 5 MINUTA u remont fazi
                            if ((DateTime.Now - item.SendToRemont).TotalMinutes >= 5)
                            {
                                item.TimeSpentInRemont = Math.Round((DateTime.Now - item.SendToRemont).TotalMinutes, 2);
                                await uredjajiZaIstorju.AddAsync(tx, item.DeviceId, item);
                                keys.Add(item.DeviceId);
                            }
                        }

                        await tx.CommitAsync();
                    }

                    //ima remonta koji treba da idu u istoriju
                    if (keys.Count > 0)
                    {
                        await historyRemontService.WriteHistoryRemontsToTable();

                        await PrijemRemont.InvokeWithRetryAsync(client => client.Channel.DeleteHistoryRemontsFromCurrentRemonts(keys));
                    }
                }
                catch (Exception ex)
                {
                    string a = ex.Message;
                }
            }
        }
    }
}
