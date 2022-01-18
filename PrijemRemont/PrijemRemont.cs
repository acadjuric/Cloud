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
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace PrijemRemont
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class PrijemRemont : StatefulService
    {
        private RemontService remontService = null;
        private MailRepository mailRepository = null;
        private CloudStorageAccount _storageAccount;
        private CloudTable _table;

        public PrijemRemont(StatefulServiceContext context)
            : base(context)
        {
            string connectionString = ConfigurationManager.AppSettings["CloudConnectionString"];
            _storageAccount = CloudStorageAccount.Parse(connectionString);

            CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
            _table = tableClient.GetTableReference("CloudProjekat");
            _table.CreateIfNotExists();

            remontService = new RemontService(this.StateManager, _table);
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


            var uredjajiNaRemontu = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");
            var uredjaji = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

            await LoadDevicesOnRemontFromTable();

            // inicijalni upis uredjaja u cloud tabelu
            //await remontService.WriteInitialDevicesToTable();

            var timer = new System.Threading.Timer((e) =>
            {
                Task.Factory.StartNew(remontService.WriteToTable, cancellationToken);

            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            while (true)
            {

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Tuple<List<string>, List<string>> emails = await mailRepository.GetBodyFromUnreadMails();
                    List<string> unreadEmails = emails.Item2;
                    List<string> sendersEmails = emails.Item1;

                    foreach (string email in unreadEmails)
                    {
                        try
                        {
                            string[] parts = email.Split('\n');
                            //int id = int.Parse(parts[0].Split(':')[1]);
                            string idOrName = parts[0].Split(':')[1];
                            int id;
                            bool isNumber = int.TryParse(idOrName, out id);

                            if (!isNumber)
                            {
                                id = await remontService.FindDeviceByName(idOrName.TrimEnd('\r','\n'));

                                //nije pronadjen uredjaj sa prosledjenim nazivom
                                if (id == -1)
                                {
                                    await mailRepository.SendEmail(sendersEmails[unreadEmails.IndexOf(email)], email, 1);
                                    continue;
                                }
                            }
                                

                            double warehouseTime = double.Parse(parts[1].Split(':')[1]);
                            double workHours = double.Parse(parts[2].Split(':')[1]);

                            if (await remontService.SendToRemont(id, warehouseTime, workHours))
                                await mailRepository.SendEmail(sendersEmails[unreadEmails.IndexOf(email)], email, 0);

                            else
                                await mailRepository.SendEmail(sendersEmails[unreadEmails.IndexOf(email)], email, 1);

                        }
                        catch
                        {
                            await mailRepository.SendEmail(sendersEmails[unreadEmails.IndexOf(email)], email, 2);

                            continue;
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (OperationCanceledException ex)
                {
                    // servis pada -> upis u bazu
                    string a = ex.Message;
                    var task = Task.Factory.StartNew(remontService.WriteToTable);
                    task.Wait();

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        private async Task LoadDevicesOnRemontFromTable()
        {
            try
            {
                //Ucitavanje uredjaja koji su posalti na remont, ali remont nije zavrsen (time Spent in remont == -1)
                var uredjajiNaRemontu = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");
                var uredjaji = await this.StateManager.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

                var uredjajiNaRemontuIzTabele = _table.CreateQuery<Remont>().AsEnumerable<Remont>().Where(item => item.PartitionKey.Equals("Remont") && item.TimeSpentInRemont.Equals(-1)).ToList();
                var uredjajiTabela = _table.CreateQuery<Device>().AsEnumerable<Device>().Where(item => item.PartitionKey.Equals("Device")).ToList();

                using (var tx = this.StateManager.CreateTransaction())
                {
                    foreach (var item in uredjajiNaRemontuIzTabele)
                    {
                        if (!await uredjajiNaRemontu.ContainsKeyAsync(tx, item.DeviceId))
                            await uredjajiNaRemontu.AddAsync(tx, item.DeviceId, item);
                    }

                    foreach (var item in uredjajiTabela)
                    {
                        if (!await uredjaji.ContainsKeyAsync(tx, item.Id))
                            await uredjaji.AddAsync(tx, item.Id, item);
                    }

                    await tx.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                //throw ex;
                return;
            }
        }
    }
}
