using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IstorijaRemont;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using PrijemRemont;

namespace Publisher
{
    public class PublisherService : IPublisher
    {
        public async Task<Tuple<List<Remont>, List<Remont>>> GetRemontAndHistoryRemont()
        {
            var binding = WcfUtility.CreateTcpClientBinding();
            var bindingHistory = WcfUtility.CreateTcpClientBinding();

            ServicePartitionClient<WcfCommunicationClient<IRemont>> prijemRemont = new ServicePartitionClient<WcfCommunicationClient<IRemont>>(
                new WcfCommunicationClientFactory<IRemont>(binding),
                new Uri("fabric:/Project3/PrijemRemont"),
                new ServicePartitionKey(0)
                );

            ServicePartitionClient<WcfCommunicationClient<IHistoryRemont>> historyRemont = new ServicePartitionClient<WcfCommunicationClient<IHistoryRemont>>(
                new WcfCommunicationClientFactory<IHistoryRemont>(bindingHistory),
                new Uri("fabric:/Project3/IstorijaRemont"),
                new ServicePartitionKey(0)
                );

            List<Remont> devicesOnRemont = await prijemRemont.InvokeWithRetryAsync(client => client.Channel.GetAllRemonts());

            List<Remont> devicesHistoryRemont = await historyRemont.InvokeWithRetryAsync(client => client.Channel.GetAllHisotryRemonts());

            return new Tuple<List<Remont>, List<Remont>>(devicesOnRemont, devicesHistoryRemont);
        }
    }
}
