using System;
using System.Collections.Generic;
using System.Configuration;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
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

namespace IstorijaRemont1
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class IstorijaRemont1 : StatelessService
    {
        private HistoryRemontService1 historyRemontService = null;

        private CloudStorageAccount _storageAccount;
        private CloudTable _table;

        public IstorijaRemont1(StatelessServiceContext context)
            : base(context)
        {
            string connectionString = ConfigurationManager.AppSettings["CloudConnectionString"];
            _storageAccount = CloudStorageAccount.Parse(connectionString);

            CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
            _table = tableClient.GetTableReference("CloudProjekat");
            _table.CreateIfNotExists();

            historyRemontService = new HistoryRemontService1(_table);
        }

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new List<ServiceInstanceListener>
            {
                new ServiceInstanceListener(context=> this.CreateListener(context), "IstorijaRemont1Endpoint"),
            };
        }

        private ICommunicationListener CreateListener(StatelessServiceContext context)
        {
            string host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("IstorijaRemont1Endpoint");
            int port = endpointConfig.Port;
            string scheme = endpointConfig.Protocol.ToString();

            string uri = string.Format(CultureInfo.InvariantCulture, "net.{0}://{1}:{2}/IstorijaRemont1Endpoint", scheme, host, port);

            var listener = new WcfCommunicationListener<IHistoryRemont1>(
                context,
                historyRemontService,
                WcfUtility.CreateTcpClientBinding(maxMessageSize: 1024 * 1024 * 1024),
                new EndpointAddress(uri)
                );

            ServiceEventSource.Current.Message("Napravljen ISTORIJA REMONT 1 listener!");

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

            var binding = WcfUtility.CreateTcpClientBinding();

            ServicePartitionClient<WcfCommunicationClient<IRemont>> PrijemRemont = new ServicePartitionClient<WcfCommunicationClient<IRemont>>(
                    new WcfCommunicationClientFactory<IRemont>(clientBinding: binding),
                    new Uri("fabric:/Project3/PrijemRemont"),
                    new ServicePartitionKey(0)
                    );

            Dictionary<int, Remont> uredjajiZaIstoriju = new Dictionary<int, Remont>();


            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    //logika za pozivanje PrijemRemont mikroservisa za dobavljanje trenutnih uredjaja na remontu radi provere
                    // uslova za upis u istorijsku bazu

                    List<Remont> uredjajiNaRemontu = await PrijemRemont.InvokeWithRetryAsync(client => client.Channel.GetAllRemonts());

                    uredjajiZaIstoriju.Clear();

                    foreach (var item in uredjajiNaRemontu)
                    {
                        //Remont je zavrsen ako je proveo bar 5 MINUTA u remont fazi
                        if ((DateTime.Now - item.SendToRemont).TotalMinutes >= 5)
                        {
                            item.TimeSpentInRemont = Math.Round((DateTime.Now - item.SendToRemont).TotalMinutes, 2);
                            //await uredjajiZaIstorju.AddAsync(tx, item.DeviceId, item);
                            uredjajiZaIstoriju.Add(item.DeviceId, item);
                        }
                    }

                    if (uredjajiZaIstoriju.Count > 0)
                    {
                        await historyRemontService.WriteHistoryRemontsToTable(uredjajiZaIstoriju);

                        await PrijemRemont.InvokeWithRetryAsync(client => client.Channel.DeleteHistoryRemontsFromCurrentRemonts(uredjajiZaIstoriju.Keys.ToList()));

                        //ciscenje kolekcije zbog pada servisa
                        uredjajiZaIstoriju.Clear();
                    }

                    ServiceEventSource.Current.ServiceMessage(this.Context, "Working Istorija Remont 1 - {0}", ++iterations);
                }
                catch (OperationCanceledException ex)
                {
                    //pad servisa -> upis u cloud tabelu i obavestavanje 'Prijem Remont-a' koji uredjaji su zavrsili sa remontom
                    string a = ex.Message;

                    if (uredjajiZaIstoriju.Count > 0)
                    {

                        await historyRemontService.WriteHistoryRemontsToTable(uredjajiZaIstoriju);

                        await PrijemRemont.InvokeWithRetryAsync(client => client.Channel.DeleteHistoryRemontsFromCurrentRemonts(uredjajiZaIstoriju.Keys.ToList()));

                        uredjajiZaIstoriju.Clear();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

            }
        }
    }
}
