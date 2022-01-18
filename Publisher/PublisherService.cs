using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IstorijaRemont1;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Wcf;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Client;
using PrijemRemont;

namespace Publisher
{
    public class PublisherService : IPublisher
    {

        ServicePartitionClient<WcfCommunicationClient<IRemont>> prijemRemont = null;
        ServicePartitionClient<WcfCommunicationClient<IHistoryRemont1>> historyRemont = null;

        public PublisherService()
        {
            var binding = WcfUtility.CreateTcpClientBinding();
            var bindingHistory = WcfUtility.CreateTcpClientBinding();

            prijemRemont = new ServicePartitionClient<WcfCommunicationClient<IRemont>>(
                new WcfCommunicationClientFactory<IRemont>(binding),
                new Uri("fabric:/Project3/PrijemRemont"),
                new ServicePartitionKey(0)
                );

            historyRemont = new ServicePartitionClient<WcfCommunicationClient<IHistoryRemont1>>(
                new WcfCommunicationClientFactory<IHistoryRemont1>(bindingHistory),
                new Uri("fabric:/Project3/IstorijaRemont1")
                );
        }


        public async Task<List<Device>> GetDevices()
        {
            try
            {
                return await prijemRemont.InvokeWithRetryAsync(client => client.Channel.GetAllDevices());
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                //throw ex;
                return null;
            }
        }

        public async Task<Tuple<List<Remont>, List<Remont>>> GetRemontAndHistoryRemont()
        {
            try
            {
                List<Remont> devicesOnRemont = await prijemRemont.InvokeWithRetryAsync(client => client.Channel.GetAllRemonts());

                List<Remont> devicesHistoryRemont = await historyRemont.InvokeWithRetryAsync(client => client.Channel.GetAllHisotryRemonts());

                return new Tuple<List<Remont>, List<Remont>>(devicesOnRemont, devicesHistoryRemont);

            }
            catch (Exception ex)
            {
                string a = ex.Message;
                //throw ex;
                return null;
            }

        }
    }
}
