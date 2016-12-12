using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Saleae
{

    public class Plum
    {
        [JsonProperty]
        private List<LightPad> LightPads; //these are the physical lightpads.
        [JsonProperty]
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
            Lights = new List<Light>();
            try
            {
                while (true)
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
                        LightPads.Add(new LightPad(new_ep.Address.MapToIPv4().ToString(), new_ep.Port, elements[2], ""));
                        Console.WriteLine("Found " + LightPads.Count() + " so far...");
                    }
                }
            }
            catch (SocketException)
            {
                //timeout exception. Normal.    
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

            string home_access_token = null;
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
                    home_access_token = house_detail.house_access_token;
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

                            foreach (string lightpad_id in load_detail.lpids)
                            {
                                light_pads.Add(LightPads.Single(x => x.Id == lightpad_id));
                            }

                            Lights.Add(new Light(
                                load_detail.llid.Value,
                                load_detail.logical_load_name.Value,
                                room_detail.rid.Value,
                                room_detail.room_name.Value,
                                house_detail.hid.Value,
                                house_detail.house_name.Value,
                                house_detail.house_access_token.Value,
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

            for (int i = 0; i < LightPads.Count(); ++i)
                LightPads[i].HomeAccessToken = home_access_token;

            Console.WriteLine("Done");
        }

        public bool LoadData(string path)
        {
            Plum loaded_plum = null;
            try
            {
                string json = System.IO.File.ReadAllText(path);
                loaded_plum = JsonConvert.DeserializeObject<Plum>(json);
            }
            catch (Exception ex)
            {
                return false;
            }

            this.Lights = loaded_plum.Lights;
            this.LightPads = loaded_plum.LightPads;
            return true;
        }

        public void SaveData(string path)
        {
            string save_content = JsonConvert.SerializeObject(this);
            System.IO.File.WriteAllText(path, save_content);
        }

        //brightness between 0 an 100.
        public void SetAllLights( double brightness )
        {
            if( brightness > 100 )
                brightness = 100;
            if( brightness < 0 )
                brightness = 0;
            
            byte level = (byte)(brightness / 100.0 * 255);
            for( int i = 0; i < Lights.Count(); ++i)
            {
                Lights[i].Brightness = level;
            }
        }

        public double[] GetAllLights()
        {
            double[] levels = new double[Lights.Count()];
            for( int i = 0; i < Lights.Count(); ++i)
            {
                levels[i] = (double)Lights[i].Brightness / 255.0 * 100.0;
            }
            return levels;
        }

        public class Light
        {
            public Light(string logicalloadid, string name, string roomid, string roomname, string houseid, string housename, string housetoken, List<LightPad> light_pads)
            {
                //parameter names probably need to match variable names in order for json to load readonly feilds.
                LogicalLoadId = logicalloadid;
                Name = name;
                RoomId = roomid;
                RoomName = roomname;
                HouseId = houseid;
                HouseName = housename;
                HouseToken = housetoken;
                LightPads = light_pads;
            }

            public readonly string LogicalLoadId;
            public readonly string Name;
            public readonly string RoomName;
            public readonly string RoomId;
            public readonly string HouseName;
            public readonly string HouseId;
            public readonly string HouseToken;
            [JsonProperty]
            public readonly List<LightPad> LightPads;
            [JsonIgnoreAttribute]
            public byte Brightness
            {
                get
                {
                    string status_json = LightPads.First().PlumCommand("getLogicalLoadMetrics", new Dictionary<string, string>() { { "llid", LogicalLoadId } });
                    dynamic status = JObject.Parse(status_json);
                    return (byte)status.level;
                }
                set
                {
                    var data = new { level = (int)value, llid = LogicalLoadId };
                    LightPads.First().PlumCommand("setLogicalLoadLevel", data);
                }
            }
        }

        public class LightPad
        {
            public LightPad(string ip, int port, string id, string homeaccesstoken)
            {
                Ip = ip;
                Port = port;
                Id = id;
                HomeAccessToken = homeaccesstoken;
            }
            public readonly string Ip;
            public readonly int Port;
            public readonly string Id;
            public string HomeAccessToken { get; set; }

            public string PlumCommand(string rest_command, object data)
            {

                string home_hash;
                using (var algorithm = SHA256.Create())
                {
                    var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(HomeAccessToken));
                    home_hash = String.Join("", hash.Select(x => x.ToString("x2")));
                }

                HttpClientHandler handler = new HttpClientHandler();

                handler.ServerCertificateCustomValidationCallback = (a, b, c, d) => { return true; };

                var client = new HttpClient(handler);

                client.DefaultRequestHeaders.UserAgent.ParseAdd("Plum/2.3.0 (iPhone; iOS 9.2.1; Scale/2.00)");
                client.DefaultRequestHeaders.Add("X-Plum-House-Access-Token", home_hash);
                string url = String.Format("https://{0}:{1}/v2/{2}", Ip, Port, rest_command);

                var request_task = client.PostAsync(url, new StringContent(
                    JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"
                ));

                string response_str = request_task.Result.Content.ReadAsStringAsync().Result;
                return response_str;
            }
        }

    }
}