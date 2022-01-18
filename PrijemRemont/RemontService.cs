using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;

namespace PrijemRemont
{
    public class RemontService : IRemont
    {
        private IReliableStateManager state = null;
        private CloudTable tabela = null;
        public RemontService(IReliableStateManager state, CloudTable tabela)
        {
            this.state = state;
            this.tabela = tabela;
        }

        public async Task DeleteHistoryRemontsFromCurrentRemonts(List<int> keys)
        {
            try
            {
                var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

                using (var tx = this.state.CreateTransaction())
                {
                    foreach (var key in keys)
                    {
                        if (await uredjajiNaRemontu.ContainsKeyAsync(tx, key))
                        {
                            //device je objekat koji sadrzi kljuc i vrednost (key value pair)
                            var device = await devices.TryGetValueAsync(tx, key);
                            if (device.HasValue)
                            {
                                device.Value.OnRemont = false;
                                await devices.SetAsync(tx, key, device.Value);
                            }

                            await uredjajiNaRemontu.TryRemoveAsync(tx, key);
                        }
                    }

                    await tx.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<List<Remont>> GetAllRemonts()
        {
            try
            {
                List<Remont> retVal = new List<Remont>();

                var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");
                using (var tx = this.state.CreateTransaction())
                {
                    var enumerator = (await uredjajiNaRemontu.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        retVal.Add(enumerator.Current.Value);
                    }
                }

                return retVal;
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<bool> SendToRemont(int id, double timeInWarehouse, double workHours)
        {
            try
            {
                if (!Validation(id, timeInWarehouse, workHours)) return false;

                var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

                using (var tx = this.state.CreateTransaction())
                {
                    //device je objekat koji sadrzi kljuc i vrednost (key value pair)
                    var device = await devices.TryGetValueAsync(tx, id);

                    if (device.HasValue == false)
                        return false;

                    if (await uredjajiNaRemontu.ContainsKeyAsync(tx, id) || device.Value.OnRemont)
                    {
                        return false;
                    }

                    Remont remont = new Remont(id)
                    {
                        DeviceId = id,
                        DeviceName = device.Value.Name,
                        HoursInWarehouse = timeInWarehouse,
                        WorkHours = workHours,
                        NumberOfRemont = new Random().Next(1, int.MaxValue),
                        SendToRemont = DateTime.Now,
                        TimeSpentInRemont = -1,
                    };

                    await uredjajiNaRemontu.AddAsync(tx, id, remont);

                    //device je objekat koji sadrzi kljuc i vrednost (key value pair)
                    device.Value.OnRemont = true;
                    await devices.SetAsync(tx, id, device.Value);

                    await tx.CommitAsync();
                }


                return true;
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task WriteToTable()
        {
            try
            {
                var uredjajiNaRemontu = await this.state.GetOrAddAsync<IReliableDictionary<int, Remont>>("RemontDevices");

                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

                using (var tx = this.state.CreateTransaction())
                {
                    //svaki batch mora da sadrzi entitete sa istim PartitionKey-om
                    TableBatchOperation batchOperationDevices = new TableBatchOperation();
                    TableBatchOperation batchOperationRemonts = new TableBatchOperation();

                    var devicesEnumerator = (await devices.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await devicesEnumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        batchOperationDevices.InsertOrMerge(devicesEnumerator.Current.Value);
                    }

                    //baca exception ako je batch prazan a pokusa se tabela.execute(batch)
                    if (batchOperationDevices.Count > 0)
                        await tabela.ExecuteBatchAsync(batchOperationDevices);

                    var enumerator = (await uredjajiNaRemontu.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        batchOperationRemonts.InsertOrMerge(enumerator.Current.Value);
                    }

                    //baca exception ako je batch prazan a pokusa se tabela.execute(batch)
                    if (batchOperationRemonts.Count > 0)
                        await tabela.ExecuteBatchAsync(batchOperationRemonts);

                }

            }
            catch (StorageException ex)
            {
                //The remote server returned an error: (409) Conflict.
                string a = ex.Message;
                throw ex;
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<List<Device>> GetAllDevices()
        {
            try
            {
                List<Device> retVal = new List<Device>();
                var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");
                using (var tx = this.state.CreateTransaction())
                {
                    var enumerator = (await devices.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                    while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                    {
                        if (enumerator.Current.Value.OnRemont == false)
                        {
                            retVal.Add(enumerator.Current.Value);
                        }
                    }
                }

                return retVal;
            }
            catch (Exception ex)
            {
                string a = ex.Message;
                throw ex;
            }
        }

        public async Task<int> FindDeviceByName(string name)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
                return -1;

            var devices = await this.state.GetOrAddAsync<IReliableDictionary<int, Device>>("Devices");

            using (var tx = this.state.CreateTransaction())
            {
                var enumerator = (await devices.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumerator.MoveNextAsync(new System.Threading.CancellationToken()))
                {
                    if (enumerator.Current.Value.Name.ToLower().Equals(name.ToLower()))
                        return enumerator.Current.Key;
                }
            }

            return -1;
        }

        private bool Validation(int id, double timeInWarehouse, double workHours)
        {
            return id <= 0 || workHours < 1 || timeInWarehouse < 0 ? false : true;
        }

        public async Task WriteInitialDevicesToTable()
        {
            List<Device> uredjaji = new List<Device>()
            {
                new Device(1){  Id = 1, Name= "PLC 2000", OnRemont= false},
                new Device(2){  Id = 2, Name= "PLC 3017", OnRemont= false},
                new Device(3){  Id = 3, Name= "PLC 4023", OnRemont= false},
                new Device(4){  Id = 4, Name= "RTU 32", OnRemont= false},
                new Device(5){  Id = 5, Name= "RTU 16", OnRemont= false},
                new Device(6){  Id = 6, Name= "RTU 64", OnRemont= false},
                new Device(7){  Id = 7, Name= "IED 200", OnRemont= false},
                new Device(8){  Id = 8, Name= "IED 500", OnRemont= false},
                new Device(9){  Id = 9, Name= "IED 300", OnRemont= false},
                new Device(10){ Id =10, Name= "HUB SWITCH 256", OnRemont= false},
                new Device(11){ Id =11, Name= "HUB SWITCH 128", OnRemont= false},
                new Device(12){ Id =12, Name= "HUB SWITCH 512", OnRemont= false},
            };

            foreach (var item in uredjaji)
            {
                try
                {
                    TableOperation insert = TableOperation.Insert(item);
                    await tabela.ExecuteAsync(insert);
                }
                catch (StorageException ex)
                {
                    string exception = ex.Message;
                    continue;
                }

            }
        }


    }
}
