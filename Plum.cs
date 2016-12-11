using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Saleae
{
    public class Plum
    {

        private List<LightPad> LightPads; //these are the physical lightpads.
        private List<Light> Lights; //these are the logical loads.
        public void ScanForDevices(string email, string password)
        {
            Socket socket = new Socket(SocketType.Dgram, ProtocolType.Unspecified);
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 50000);
            socket.EnableBroadcast = true;
            socket.ExclusiveAddressUse = false;
            socket.ReceiveTimeout = 5000;
            socket.SendTimeout = 5000;
            socket.Bind(endpoint);

            IPEndPoint broadcast_ep = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 43770);
            byte[] broadcast_data = "PLUM".Select(x => Convert.ToByte(x)).ToArray();

            socket.SendTo(broadcast_data, 0, broadcast_data.Count(), SocketFlags.None, broadcast_ep);

            LightPads = new List<LightPad>();

            try
            {
                // while (true)
                {
                    EndPoint response_ep = new IPEndPoint(IPAddress.Any, 0);
                    byte[] rx_buffer = new byte[2048];
                    int rx_count = socket.ReceiveFrom(rx_buffer, ref response_ep);
                    string response = Encoding.UTF8.GetString(rx_buffer.Take(rx_count).ToArray());
                    string[] elements = response.Split(' ');
                    string lightpad_id = elements[2];
                    if (LightPads.Any(x => x.Id == lightpad_id) == false)
                    {
                        IPEndPoint new_ep = new IPEndPoint((response_ep as IPEndPoint).Address, int.Parse(elements[3]));
                        LightPads.Add(new LightPad(new_ep, elements[2]));
                        Console.WriteLine("Found " + LightPads.Count() + " so far...");
                    }
                }
            }
            catch (SocketException ex)
            {
                //timeout exception. Normal.    
                //Console.WriteLine("socket error: " + ex.Message);
            }
            finally
            {
                socket.Dispose();
            }
            Console.WriteLine("finished searching for light pads");

            HttpClient client = new HttpClient();

            var auth_bytes = Encoding.ASCII.GetBytes(email + ":" + password);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Plum/2.3.0 (iPhone; iOS 9.2.1; Scale/2.00)");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(auth_bytes));


            try
            {
                var response = client.GetAsync("https://production.plum.technology/v2/getHouses"); //returns an array of house IDs in JSON format.
                string house_list_json = response.Result.Content.ReadAsStringAsync().Result;
                var house_list = Newtonsoft.Json.Linq.JArray.Parse(house_list_json).Values<string>();

                foreach (string house_id in house_list)
                {
                    response = client.PostAsync("https://production.plum.technology/v2/getHouse", new StringContent(
                        JsonConvert.SerializeObject(new { hid = house_id }), Encoding.UTF8, "application/json"
                    ));
                    dynamic house_detail = JObject.Parse(response.Result.Content.ReadAsStringAsync().Result);

                    foreach (string room_id in house_detail.rids)
                    {
                        response = client.PostAsync("https://production.plum.technology/v2/getRoom", new StringContent(
                            JsonConvert.SerializeObject(new { rid = room_id }), Encoding.UTF8, "application/json"
                        ));
                        dynamic room_detail = JObject.Parse(response.Result.Content.ReadAsStringAsync().Result);

                        foreach (string load_id in room_detail.llids)
                        {
                            response = client.PostAsync("https://production.plum.technology/v2/getLogicalLoad", new StringContent(
                                JsonConvert.SerializeObject(new { llid = load_id }), Encoding.UTF8, "application/json"
                            ));
                            dynamic load_detail = JObject.Parse(response.Result.Content.ReadAsStringAsync().Result);
                            Console.WriteLine("Logical load detail: " + load_detail);
                            Console.WriteLine("room detail: " + room_detail);
                            Console.WriteLine("house detail: " + house_detail);
                            List<LightPad> light_pads = new List<LightPad>();
 
                            foreach( string lightpad_id in load_detail.lpids)
                            {
                                light_pads.Add(LightPads.Single( x => x.Id == lightpad_id ));
                            }
                            Lights.Add( new Light(
                                load_detail.llid,
                                load_detail.logical_load_name,
                                room_detail.rid,
                                room_detail.room_name,
                                house_detail.hid,
                                house_detail.house_name,
                                house_detail.house_access_token,
                                light_pads
                            ));
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                Console.WriteLine("failed: " + ex.Message);
            }

            Console.WriteLine("Done");

        }

        public void LoadData(string path)
        {

        }

        public void SaveData(string path)
        {

        }




        public class Light
        {
            public Light( string id, string name, string room_id, string room_name, string house_id, string house_name, string house_token, List<LightPad> light_pads )
            {
                LogicalLoadId = id;
                Name = name;
                RoomId = room_id;
                RoomName = room_name;
                HouseId = house_id;
                HouseName = house_name;
                HouseToken = house_token;
                LightPads = light_pads;
            }

            public readonly string LogicalLoadId;
            public readonly string Name;
            public readonly string RoomName;
            public readonly string RoomId;
            public readonly string HouseName;
            public readonly string HouseId;
            public readonly string HouseToken;
            public readonly List<LightPad> LightPads;

            public byte Brightness
            {
                get; set;
            }

        }

        public class LightPad
        {
            public LightPad(EndPoint endpoint, string id)
            {
                Endpoint = endpoint;
                Id = id;
            }

            public readonly EndPoint Endpoint;
            public readonly string Id;

            public void PlumCommand(string url, Dictionary<string, string> data)
            {
                var client = new HttpClient();

                var request_task = client.PostAsync("", new FormUrlEncodedContent(data));

                request_task.Wait();
                var response = request_task.Result;
                string response_str = response.Content.ReadAsStringAsync().Result;

            }
        }
    }
}