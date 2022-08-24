using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace PNB.Helpers
{
    public enum DiskSpace { VeryLow, Low, High, VeryHigh }
    public enum FtpAction { DownloadSettings, DownloadAppFiles }
    public enum Operation { Add, Delete, Details, Reset, Rename, Stop, Start, Change, AutoStart, Close }
    public class Util
    {
        public const string NoIP = "N/A";
        public static T DeepClone<T>(T obj)
        {
            T t1 = default;
            System.IO.MemoryStream ms = null;
            try
            {
                ms = new System.IO.MemoryStream();
                var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                t1 = (T)formatter.Deserialize(ms);
            }
            catch (Exception eee)
            {
                Logger.Log(eee);
            }

            return t1;
        }


        static readonly Random rnd = new Random();
        static readonly double max1 = 100000000000000000;

        public static long GetLong()
        {
            //long l = 0;
            //byte[] buf = new byte[8];
            //rnd.NextBytes(buf);
            long longRand = (long)(rnd.NextDouble() * max1);
            //long longRand = BitConverter.ToInt64(buf, 0);
            //l = (Math.Abs(longRand % (max - min)) + min);
            //return l;
            return longRand;
        }
        public static int GetRandomRangeInt(int min, int max)
        {
            return rnd.Next(min, max);
        }

        #region Date Time
        /// <summary>
        /// HH:mm:ss.fff
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static string ConvertTimeToString(long time, bool includeMilli)
        {
            if (time <= 0) return "Negative Time!";
            if (time >= DateTime.MaxValue.Ticks) return "Overflow Time!";

            DateTime dt = new DateTime(time);
            string dd = ConvertTimeToString(dt, includeMilli);

            return dd;
        }
        public static string ConvertTimeToString(DateTime dt, bool includeMilli)
        {
            string dd = includeMilli ? dt.ToString("HH:mm:ss.fff") : dt.ToString("HH:mm:ss");

            return dd;
        }

        /// <summary>
        /// yyyy-mm-dd HH.MM.ss.fff Or yyyy-mm-dd HH.MM.ss
        /// </summary>
        /// <param name="includeMilli"></param>
        /// <returns></returns>
        public static string ConvertDateTimeToFileName(bool includeMilli)
        {
            return ConvertDateTimeToFileName(DateTime.Now, includeMilli);
        }



        /// <summary>
        /// yyyy-mm-dd HH.MM.ss.fff Or yyyy-mm-dd HH.MM.ss
        /// </summary>
        /// <param name="time"></param>
        /// <param name="includeMilli"></param>
        /// <returns></returns>
        public static string ConvertDateTimeToFileName(long time, bool includeMilli)
        {
            if (time <= 0) return "Negative Time!";
            if (time >= DateTime.MaxValue.Ticks) return "Overflow Time!";

            DateTime ts = new DateTime(time);
            return ConvertDateTimeToFileName(ts, includeMilli);
        }
        /// <summary>
        /// yyyy-mm-dd HH.MM.ss.fff Or yyyy-mm-dd HH.MM.ss
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="includeMilli"></param>
        /// <returns></returns>
        public static string ConvertDateTimeToFileName(DateTime dt, bool includeMilli)
        {
            string dd = "";
            if (PersianCalendar.CT == PersianCalendar.CalendarType.Georgian)
                dd = includeMilli ? dt.ToString("yyyy-MM-dd_HH.mm.ss.ffffff") : dt.ToString("yyyy-MM-dd_HH.mm.ss");
            else
                dd = PersianCalendar.GregorianToJalali(dt.Ticks, true, includeMilli);

            return dd;
        }

        #endregion

        public static void Serialize(object graph, string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                try
                {
                    formatter.Serialize(fs, graph);
                }
                catch (SerializationException e)
                {
                    Logger.Log(e);
                }
            }
        }

        #region Scripts

        public static string ExecuteCommand(string command, string argument = "", bool useShell = true)
        {
            Logger.Debug($"Executing a command. CMD:'{command}'.   ARG:'{argument}'.   Use Shell: '{useShell}'");

            // according to: https://stackoverflow.com/a/15262019/637142
            // thanks to this, we will pass everything as one command
            //command = command.Replace("\"", "\"\"");
            //if (executeWithSudo)
            //    if (!command.ToLower().StartsWith("sudo"))
            //        command = "sudo " + command;

            string res = "";
            Process p = null;
            string randomfile = string.Empty;
            ProcessStartInfo psi = new ProcessStartInfo();
            if (IsLinux)
            {
                if (useShell)
                {
                    randomfile = Configuration.GetRamDiskDir() + Path.GetRandomFileName();
                    psi.FileName = "/bin/bash";
                    psi.Arguments = string.Format("-c '{0} >> {1}'", command, randomfile);
                    Logger.Debug("psi.Arguments = " + psi.Arguments);
                }
                else
                {
                    psi.FileName = command;
                    psi.Arguments = argument;
                }
            }
            else if (IsWindows)
            {
                if (useShell)
                {
                    randomfile = "";
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c \"{command}\"";
                }
                else
                {
                    psi.FileName = command;
                    psi.Arguments = argument;
                }
            }

            /// FROM MSDN: UseShellExecute must be false if the UserName property is not null or an empty string,
            /// or an InvalidOperationException will be thrown when the Process.Start(ProcessStartInfo) method is called.
            psi.UseShellExecute = useShell;
            /// FROM MSDN: You must set UseShellExecute to false if you want to set RedirectStandardOutput to true.
            ///Otherwise, reading from the StandardOutput stream throws an exception.                       
            psi.RedirectStandardOutput = !useShell;// !useShell && string.IsNullOrEmpty(psi.UserName.Trim());
            //psi.CreateNoWindow = true;
            psi.WorkingDirectory = Configuration.GetAppPath();
            psi.WindowStyle = ProcessWindowStyle.Normal;

            try
            {
                p = Process.Start(psi);
                if (psi.RedirectStandardOutput)
                    res = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                if (useShell)
                {
                    res = File.ReadAllText(randomfile);
                    File.Delete(randomfile);
                }
            }
            catch (Exception ee)
            {
                res = ee.ToString();
                Logger.Debug($"Err: CMD:'{command}'. Args:'{argument}'. UseShell:'{useShell}'.\n" + ee.Message);
            }
            finally
            {
                if (p != null)
                {
                    //p.CloseMainWindow();
                    p.Close();
                }
            }
            //Logger.Debug("res=" + res);
            return res;
        }

        public static long LaunchMFRC522(out string data)
        {
            Logger.Debug("LaunchMFRC522");
            data = "";
            long tag = -1;
            try
            {
                string args = Configuration.GetRFIDScriptPath();
                data = Util.ExecuteCommand("python", string.Format("-u {0}", args), false);//LaunchMFRC522

                if (data.Contains("ID:"))
                {
                    int idx = data.IndexOf("ID:") + 3;
                    string h = data.Substring(idx, data.Length - idx);
                    if (!long.TryParse(h, out tag))
                        tag = -1;
                }
            }
            catch (Exception rrr)
            {
                Logger.Log(rrr);
            }

            Logger.Debug("LaunchMFRC522 finished.");
            return tag;
        }
        #endregion

        #region System
        public static void ShutDownDevice()
        {
            if (IsLinux)
                Util.ExecuteCommand("sudo halt");
            if (IsWindows)
                Util.ExecuteCommand("shutdown /s");
        }
        public static void ResetDevice()
        {
            if (IsLinux)
                Util.ExecuteCommand("sudo reboot");
            if (IsWindows)
                Util.ExecuteCommand("shutdown /r");
        }
        #endregion
        public static void OpenContainingFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            if (IsLinux)
                Process.Start("", path);
            else
                Process.Start("explorer.exe", path);
        }
        public static Form GetAppForm<T>()
        {
            foreach (Form f in Application.OpenForms)
                if (f.GetType() == typeof(T))
                    return f;
            return null;
        }

        public static Version GetAppVersion(System.Reflection.Assembly asm)
        {
            return asm.GetName().Version;
        }


        #region Zip
        public static string Zip(string filePath)
        {
            string result = filePath + ".gz";
            using (FileStream inFile = File.OpenRead(filePath))
            {
                using (FileStream outFile = File.Create(result))
                {
                    GZipStream z = new GZipStream(outFile, CompressionMode.Compress);
                    inFile.CopyTo(z);
                }
            }
            return result;
        }
        public static byte[] Zip(byte[] in_bytes)
        {
            MemoryStream out_ms = new MemoryStream();
            GZipStream z = new GZipStream(out_ms, CompressionMode.Compress);
            z.Write(in_bytes, 0, in_bytes.Length);
            z.Close();
            byte[] bts = out_ms.ToArray();
            out_ms.Close();
            File.WriteAllBytes("dev_" + DateTime.Now.Ticks, bts);

            return bts;
        }
        public static byte[] UnZip(byte[] in_bytes)
        {
            File.WriteAllBytes("server_" + DateTime.Now.Ticks, in_bytes);

            byte[] bts = null;
            using (var compressedStream = new MemoryStream(in_bytes))
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var resultStream = new MemoryStream())
            {
                zipStream.CopyTo(resultStream);
                bts = resultStream.ToArray();
            }

            return bts;
        }
        #endregion

        #region Disk
        public static DiskSpace GetDiskSpaceStatus()
        {
            DiskSpace ds = DiskSpace.VeryLow;

            return ds;
        }
        #endregion

        #region Network

        #region WiFi
        ///https://en.wikipedia.org/wiki/Wireless_tools_for_Linux
        ///https://www.raspberrypi.org/documentation/computers/configuration.html#configuring-networking
        ///https://superuser.com/questions/748362/how-do-i-choose-which-router-to-connect-to-with-the-same-ssid/748382#748382         
        ///https://www.raspberrypi.org/documentation/computers/configuration.html#using-the-command-line
        ///https://unix.stackexchange.com/questions/415816/i-am-trying-to-connect-to-wifi-using-wpa-cli-set-network-command-but-it-always-r                               
        ///https://unix.stackexchange.com/questions/501200/using-and-escaping-wpa-cli-set-network-command-in-a-bash-script

        /// Linux Network/Wireless commands:
        /// iw              - show / manipulate wireless devices and their configuration
        ///     iw dev
        /// iwconfig/iwlist - configure a wireless network interface
        /// rfkill          - tool for enabling and disabling wireless devices
        /// sudo ifconfig wlan0 up/Down
        /// sudo iwgetid
        /// sudo iwlist wlan0 scan

        public static bool IsWiFiDown(string net_interface)
        {
            if (isLinux)
                return Util.ExecuteCommand($"ip add show {net_interface}").Contains("DOWN");
            return false;
        }
        public static void WiFiUp()
        {
            Util.ExecuteCommand("sudo ifconfig wlan0 up");
        }
        public static void WiFiDown()
        {
            Util.ExecuteCommand("sudo ifconfig wlan0 down");
        }
        public static bool WiFiIsConnected()
        {
            ///Docs
            /// https://en.wikipedia.org/wiki/Wireless_tools_for_Linux
            ///  Rpi docs @ https://www.raspberrypi.org/documentation/computers/configuration.html#configuring-networking
            ///  uses 'ifconfig wlan0' instead.

            bool result = false;
            string cmd = "";
            if (IsLinux)
            {
                cmd = "sudo iwgetid";
                string res = ExecuteCommand(cmd);

                result = res.Contains("ESSID");
            }
            else if (IsWindows)
            {
            }
            return result;
        }
        public static string WiFiGetCurrentConnectedSSID()
        {
            string msg = "";
            string result = "";
            if (Util.IsLinux)
            {
                string command = $"sudo iwgetid";
                msg = ExecuteCommand(command);
                msg = msg.Trim();
                if (!string.IsNullOrEmpty(msg))
                {
                    msg = msg.Substring(17).Replace("\"", string.Empty).Trim();
                    result = msg;
                }
            }
            else if (Util.isWindows)
            {
                msg = "";
            }
            Logger.Debug($"WiFiGetCurrentConnectedSSID ='{result}'");
            return result;
        }
        public static string[] WiFiGetAvailableSSIDs(string nic)
        {
            List<string> ssids = new List<string>();
            string msg = "";

            if (IsLinux)
            {
                string command = $"sudo iwlist {nic} scan";
                //Logger.Debug(command);
                msg = ExecuteCommand(command);
                //Logger.Debug("end");
            }
            else if (IsWindows)
            {
                ///From https://gist.github.com/lmcarreiro/cb67f6695b1ed78a9ce281bdcb51b4bc
                /// Question: Should we include 'mode=bssid'?
                msg = ExecuteCommand("netsh.exe wlan show networks");
            }

            string[] lines = msg.Split(Defs.Splitter10, StringSplitOptions.RemoveEmptyEntries);
            //lines = msg.Split(Defs.Splitter101, StringSplitOptions.RemoveEmptyEntries);
            //Console.WriteLine(lines.Length);
            foreach (string line in lines)
                if (line.Contains("SSID"))
                {
                    string ssid = "";
                    if (isLinux)
                        ssid = line.Replace("ESSID:", string.Empty).Replace("\"", string.Empty).Trim();
                    else if (isWindows)
                    {
                        ssid = line.Split(Defs.Splitter6, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(ssid))
                    {
                        //Debug(ssid);
                        ssids.Add(ssid);
                    }
                }

            return ssids.ToArray();
        }
        public static bool WiFiForgetSSID(string ssid)
        {
            if (string.IsNullOrEmpty(ssid)) return false;
            string msg = "";
            string command = "";

            if (IsLinux)
            {
                //// Docs
                /// Steps o forget an ssid
                /// 1 - sync wpa__cli and wpa conf file
                /// 2 - get network id of 'ssid' from cli
                /// 3 - remove 'ssid'
                /// 4 - save cli back to the file
                /// 5 - reconfigure

                /// 1 - sync
                command = "sudo wpa_cli reconfigure"; ///force to read conf file
                msg = ExecuteCommand(command);
                /// 2 - get network id of 'ssid'
                string[] id_as_str = WiFiGetWpaNetworkID(ssid);
                Logger.Debug("network id count: " + id_as_str.Length);
                if (id_as_str.Length == 0)
                {
                    Logger.Debug($"'{ssid}' not found!");
                    return false;
                }

                Array.ForEach<string>(id_as_str, delegate (string x)
                {
                    Logger.Debug("3-id_as_str:'" + x + "'");
                    /// 3 - remove from wpa supplicant
                    command = $"sudo wpa_cli remove_network {x}";
                    ExecuteCommand(command);
                });

                /// 4 - save back
                command = $"sudo wpa_cli save_config";
                msg = ExecuteCommand(command);

                command = "sudo wpa_cli reconfigure";
                msg = ExecuteCommand(command);
            }
            return true;
        }
        public static bool WiFiConnectToNetwork(string ssid, string psk)
        {
            bool res = true;
            string command = "";

            if (IsLinux)
            {
                //// Docs
                /// Steps to connect to an ssid
                /// 1 - sync wpa__cli and wpa conf file
                /// 2 - if exists, forget 'ssid'
                /// 3 - add network
                /// 4 - enbale it
                /// 5 - save cli back to the file
                /// 6 - reconfigure
                ///
                /// When we call reconfigure, wpa subsystem autmatically connects to it.

                Logger.Debug("Step 1: sync wpa__cli and wpa conf file");

                //if already connected, do not connect again!
                if (WiFiGetCurrentConnectedSSID() == ssid)
                {
                    Logger.Debug("Already Connected!");
                    return false;
                }

                /// According to wpa_cli usage help
                if (psk.Length < 8 || psk.Length > 63)
                {
                    Logger.Debug("Passphrase must be 8..63 characters");
                    return false;
                }
                Logger.Debug("WiFiForgetSSID: " + WiFiForgetSSID(ssid));


                //string command = $"sudo wpa_passphrase {ssid} {psk}";                
                //string psk1 = ExecuteCommand(command);                
                //psk1 = psk1.Split(Defs.Splitter10, StringSplitOptions.None)[3].Trim().Replace("psk=", string.Empty);

                Logger.Debug("Step 1: Add Network");
                command = "sudo wpa_cli add_network";
                string msg = ExecuteCommand(command);
                Logger.Debug("add_network msg = " + msg);
                if (msg.Contains("FAIL"))
                {
                    Logger.Debug("Add network failed!");
                    return false;
                }

                string[] lines = msg.Split(Defs.Splitter10, StringSplitOptions.None);
                string networkId = lines[1].Trim();
                Logger.Debug("networkId:" + networkId);

                /// wpa_cli set_netwrok acceptes argument as follows:
                ///  \"Dlink123\".
                ///  Full correct format:
                ///  sudo wpa_cli set_network 12 ssid \"Dlink123\";
                ///  While compiling, c# escapes the whole string into this string:
                ///  sudo wpa_cli set_network 12 ssid \"Dlink123\"
                ///  if we escape backslash and double-qoute, then we have
                ///  sudo wpa_cli set_network 12 ssid \\\"Dlink123\\\",
                ///  but again, Process clas itself, escapes backslash.
                ///  Finally on Linux, what works is this:
                ///  sudo wpa_cli set_network 12 ssid \\\\\"Dlink123\\\\\"

                string ff = "";
                command = $"sudo wpa_cli set_network {networkId} ssid \\\\\"{ssid}\\\\\"";
                msg = ExecuteCommand(command); //Console.WriteLine("3-msg:" + msg);
                Logger.Debug("set_network msg = " + msg);
                if (msg.Contains("FAIL"))
                {
                    Logger.Debug("set_network failed!");
                    return false;
                }

                if (!string.IsNullOrEmpty(psk))
                    //command = $"sudo wpa_cli set_network {networkId} psk \"\\\"{psk}\\\"\"";
                    command = $"sudo wpa_cli set_network {networkId} psk \\\\\"{psk}\\\\\"";
                else
                    command = $"sudo wpa_cli set_network {networkId} key_mgmt NONE";

                msg = ExecuteCommand(command); Logger.Debug("4-msg:" + msg);
                if (msg.Contains("FAIL")) return false;

                command = $"sudo wpa_cli enable_network {networkId}";
                msg = ExecuteCommand(command); Logger.Debug("5-msg:" + msg);
                if (msg.Contains("FAIL")) return false;

                command = $"sudo wpa_cli save_config";
                msg = ExecuteCommand(command); Logger.Debug("6-msg:" + msg);
                if (msg.Contains("FAIL")) return false;

                command = "sudo wpa_cli reconfigure";
                msg = ExecuteCommand(command); Logger.Debug("7-msg:" + msg);
                if (msg.Contains("FAIL")) return false;

                return res;
            }

            return false;
        }
        public static string[] WiFiGetSavedSSIDs()
        {
            List<string> ssids = new List<string>();
            if (IsLinux)
            {
                string command = "sudo wpa_cli list_networks";
                string msg = ExecuteCommand(command);

                //string[] fff = new string[] { Defs.Splitter10[0].Replace("\r", "") };
                string[] lines = msg.Split(Defs.Splitter10, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length == 2)//no network
                    return ssids.ToArray();

                Logger.Debug("Ver 1.7.5");
                for (int i = 2; i < lines.Length; i++)
                {
                    Logger.Debug(lines[i]);
                    string[] chks = lines[i].Split(Defs.Splitter9, StringSplitOptions.None);
                    if (chks.Length < 2) continue;

                    if (!string.IsNullOrEmpty(chks[1].Trim()))
                    {
                        Logger.Debug(chks[0].Trim());
                        ssids.Add(chks[0].Trim());
                    }
                }
            }
            else if (IsWindows)
            {
            }

            Array.ForEach<string>(ssids.ToArray(), delegate (string x)
            { Logger.Debug(x); });
            return ssids.ToArray();
        }
        public static string[] WiFiGetWpaNetworkID(string ssid)
        {
            List<string> ids = new List<string>();

            if (string.IsNullOrEmpty(ssid)) return ids.ToArray();

            string command = "sudo wpa_cli list_networks";
            string msg = ExecuteCommand(command);
            Logger.Debug("Networks list: " + msg);
            string[] lines = msg.Split(Defs.Splitter10, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 2)//no network
                return ids.ToArray();

            for (int i = 2; i < lines.Length; i++)
            {
                Logger.Debug("Line: '" + lines[i] + "'");
                string[] chks = lines[i].Split(new[] { '\t' }, StringSplitOptions.None);
                if (chks.Length < 2)
                {
                    //Array.ForEach<string>(chks, delegate (string hh) { Logger.Debug("555-" + hh); });
                    Logger.Debug("Len is less than 2! LEN:" + chks.Length);
                    continue;
                }
                Logger.Debug(chks[0] + ":" + chks[1]);

                ///Only remove those networks that their name is $ssid
                if (chks[1].Trim() == ssid)
                    ids.Add(chks[0].Trim());
            }
            return ids.ToArray();
        }
        #endregion


        public static string GetIP(bool acceptLoppback)
        {
            string ip = NoIP;
            IPHostEntry ipEntry = Dns.GetHostEntry("");
            IPAddress[] addr = ipEntry.AddressList;

            for (int i = 0; i < addr.Length; i++)
            {
                IPAddress ipa = addr[i];

                if (!acceptLoppback)
                    if (IPAddress.IsLoopback(ipa)) continue;

                if (ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    ip = addr[i].ToString();
                    break;
                }
            }
            return ip;
        }
        public static bool SetStaticIP(string ni, string ip, string netmask, string gateway)
        {
            bool res = false;
            string dhcpFile = Configuration.GetdhcpcdFilePath();
            string dhcpcd = "";
            Logger.Debug($"NI:{ni}. IP:{ip}. netmask: {netmask}. gateway:{gateway}");
            try
            {
                dhcpcd = File.ReadAllText(dhcpFile);
                IPAddress ipadd = null;
                if (!IPAddress.TryParse(ip, out ipadd)) return false;

                uint subnet = SubnetToCIDR(netmask);
                if (subnet == 1000) return false;

                dhcpcd = dhcpcd.Replace("__STATIC_IP__", ip + "/" + subnet);
                dhcpcd = dhcpcd.Replace("__INTERFACE__", ni);
                //File.WriteAllText("/home/pi/pnb/" + Path.GetRandomFileName(), dhcpcd);
                //Logger.Debug("PWD:" + Environment.CurrentDirectory);

                if (IsLinux)
                    File.WriteAllText("/etc/" + Defs.dechcp_conf, dhcpcd);
                //else
                //    File.WriteAllText("hhhh.conf", dhcpcd);

                //restart deamon
                //string msg = ExecuteCommand($"sudo ifconfig {ni} {ip} netmask {netmask} up");
                //Logger.Debug("deamon: " + msg);
                res = true;
            }
            catch (Exception ee) { Logger.Log(ee); }
            return res;
        }

        public static string CIDRToSubnet(string netMask)
        {
            /// Method taken from 
            /// https://www.codeproject.com/Messages/5293194/Vote-of-3
            string subNetMask = string.Empty;

            int calSubNet = 0;
            if (int.TryParse(netMask, out calSubNet))
            {
                if (calSubNet == 0) return "0.0.0.0";
                uint mask = (UInt32)((1) << (32 - calSubNet));
                var result = UInt32.MaxValue - mask + 1;
                subNetMask = "" + (result >> 24) + "."
                    + ((result >> 16) & 0xFF) + "."
                    + ((result >> 8) & 0xFF) + "."
                    + (result & 0xFF);
            }
            return subNetMask;
        }
        public static UInt32 SubnetToCIDR(string subnetStr)
        {

            ///https://stackoverflow.com/questions/2507950/given-an-ip-address-and-subnetmask-how-do-i-calculate-the-cidr
            IPAddress subnetAddress = null;
            UInt32 subnetConsecutiveOnes = 1000;
            try
            {
                subnetAddress = IPAddress.Parse(subnetStr);
                byte[] ipParts = subnetAddress.GetAddressBytes();
                UInt32 subnet = 16777216 * Convert.ToUInt32(ipParts[0]) + 65536 * Convert.ToUInt32(ipParts[1]) + 256 * Convert.ToUInt32(ipParts[2]) + Convert.ToUInt32(ipParts[3]);
                UInt32 mask = 0x80000000;

                subnetConsecutiveOnes = 0;
                for (int i = 0; i < 32; i++)
                {
                    if (!(mask & subnet).Equals(mask)) break;

                    subnetConsecutiveOnes++;
                    mask = mask >> 1;
                }
            }
            catch (Exception ee) { Logger.Debug(ee); }


            return subnetConsecutiveOnes;
        }


        public static string getSubnetAddressFromIPNetMask(string netMask)
        {
            string subNetMask = string.Empty;
            int calSubNet = 0;

            if (int.TryParse(netMask, out calSubNet))
            {
                if (calSubNet == 0) return "0.0.0.0";
                uint mask = (UInt32)((1) << (32 - calSubNet));
                var result = UInt32.MaxValue - mask + 1;
                subNetMask = (result >> 24) + "."
                    + ((result >> 16) & 0xFF) + "."
                    + ((result >> 8) & 0xFF) + "."
                    + (result & 0xFF);
            }
            return subNetMask;
        }
        public static string ReturnDefaultSubnetmask(string ipaddress)
        {
            string snm = "Error!";
            /// docs by behzad @ 1400/08/17
            /// https://weblogs.asp.net/razan/finding-subnet-mask-from-ip4-address-using-c
            System.Net.IPAddress iPAddress = null;

            try
            {
                iPAddress = System.Net.IPAddress.Parse(ipaddress);
                byte[] byteIP = iPAddress.GetAddressBytes();
                //uint ipInUint = (uint)byteIP[0];

                uint firstOctet = (uint)byteIP[0]; ;// ReturnFirtsOctet(ipaddress);
                if (firstOctet >= 0 && firstOctet <= 127)
                    snm = "255.0.0.0";
                else if (firstOctet >= 128 && firstOctet <= 191)
                    snm = "255.255.0.0";
                else if (firstOctet >= 192 && firstOctet <= 223)
                    snm = "255.255.255.0";
                else snm = "0.0.0.0";
            }
            catch { }

            return snm;
        }

        public static string[] GetInterfaces(NetworkInterfaceType nictype)
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            List<string> lst_nics = new List<string>();
            Array.ForEach<NetworkInterface>(nics, delegate (NetworkInterface ni)
            {
                if (ni.NetworkInterfaceType == nictype)
                    lst_nics.Add(ni.Description);
            });

            return lst_nics.ToArray();
        }

        /// <summary>
        /// returns all NICs except the loopbacks
        /// </summary>
        /// <returns></returns>
        public static string[] GetInterfaces()
        {
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            List<string> lst_nics = new List<string>();
            Array.ForEach<NetworkInterface>(nics, delegate (NetworkInterface ni)
            {
                //if (ni.NetworkInterfaceType == nictype)
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    lst_nics.Add(ni.Description);
            });

            return lst_nics.ToArray();
        }
        public static Tuple<string, string>[] GetIPv4Address(string nic)
        {
            List<Tuple<string, string>> lst = new List<Tuple<string, string>>();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            if (nics.Length == 0)
            {
                Logger.Debug("Machine has no interface!");
                return lst.ToArray();
            }

            NetworkInterface adapter = Array.Find<NetworkInterface>(nics, delegate (NetworkInterface ni)
             {
                 if (ni.Description == nic)
                     return true;

                 return false;

             });
            if (adapter == null)
            {
                Logger.Debug($"No matching interface!({nic})");
                return lst.ToArray();
            }
            UnicastIPAddressInformationCollection ucipad = adapter.GetIPProperties().UnicastAddresses;
            if (ucipad.Count == 0)
            {
                Logger.Debug("Interface has no IP!");
                return lst.ToArray();
            }

            foreach (UnicastIPAddressInformation ip in ucipad)
            {
                Logger.Debug("ip=" + ip.Address);
                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    Tuple<string, string> t = Tuple.Create<string, string>(ip.Address.ToString(), ip.IPv4Mask.ToString());
                    lst.Add(t);
                }
            }

            return lst.ToArray();

        }

        public static string[] ShowNetworkInterfaces2()
        {
            //ip link
            /// https://www.raspberrypi.org/documentation/computers/configuration.html#configuring-networking
            string cmd = "ip link";
            string res = ExecuteCommand(cmd);

            //TODO: this code is incomplete
            return null;

        }
        //public static void ShowNetworkInterfaces()
        //{
        //    IPGlobalProperties comp = IPGlobalProperties.GetIPGlobalProperties();
        //    NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
        //    Logger.Debug(string.Format("Interface information for {0}.{1}     ", comp.HostName, comp.DomainName));

        //    if (nics == null || nics.Length < 1)
        //    {
        //        Logger.Debug("  No network interfaces found.");
        //        return;
        //    }

        //    Logger.Debug($"  Number of interfaces .................... : { nics.Length}");
        //    foreach (NetworkInterface adapter in nics)
        //    {
        //        if (adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) continue;

        //        IPInterfaceProperties properties = adapter.GetIPProperties();
        //        IPv4InterfaceProperties ip4 = properties.GetIPv4Properties();
        //        UnicastIPAddressInformationCollection ucipad = properties.UnicastAddresses;
        //        Logger.Debug("");
        //        Logger.Debug(adapter.Description);
        //        Logger.Debug(String.Empty.PadLeft(adapter.Description.Length, '='));
        //        Logger.Debug($"  Interface type .......................... : {adapter.NetworkInterfaceType}");
        //        Logger.Debug($"  Physical Address ........................ : {adapter.GetPhysicalAddress()}");
        //        Logger.Debug($"  Operational status ...................... : {adapter.OperationalStatus}");
        //        Logger.Debug($"  Speed ................................... : {adapter.Speed}");
        //        Logger.Debug($"  SupportsMulticast ....................... : {adapter.SupportsMulticast}");
        //        Logger.Debug($"  Description ............................. : {adapter.Description}");
        //        Logger.Debug($"  Description ............................. : {adapter.Description}");
        //        string versions = "";
        //        Logger.Debug($"  #Address  ................................ : {ucipad.Count}");

        //        foreach (UnicastIPAddressInformation ip in ucipad)
        //            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        //                Logger.Debug($"  IP ............................. : {ip.Address.ToString()}");



        //        // Create a display string for the supported IP versions.
        //        if (adapter.Supports(NetworkInterfaceComponent.IPv4))
        //        {
        //            versions = "IPv4";
        //        }
        //        if (adapter.Supports(NetworkInterfaceComponent.IPv6))
        //        {
        //            if (versions.Length > 0)
        //            {
        //                versions += " ";
        //            }
        //            versions += "IPv6";
        //        }
        //        Logger.Debug($"  IP version .............................. : {versions}");
        //        //ShowIPAddresses(properties);

        //        // The following information is not useful for loopback adapters.
        //        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
        //        {
        //            continue;
        //        }
        //        Logger.Debug($"  DNS suffix .............................. : { properties.DnsSuffix}");

        //        string label;
        //        if (adapter.Supports(NetworkInterfaceComponent.IPv4))
        //        {
        //            IPv4InterfaceProperties ipv4 = properties.GetIPv4Properties();
        //            Logger.Debug($"  MTU...................................... : { ipv4.Mtu}");
        //            if (ipv4.UsesWins)
        //            {

        //                IPAddressCollection winsServers = properties.WinsServersAddresses;
        //                if (winsServers.Count > 0)
        //                {
        //                    label = "  WINS Servers ............................ :";
        //                    //ShowIPAddresses(label, winsServers);
        //                }
        //            }
        //        }

        //        Logger.Debug($"  DNS enabled ............................. : { properties.IsDnsEnabled}");
        //        Logger.Debug($"  Dynamically configured DNS .............. : { properties.IsDynamicDnsEnabled}");
        //        Logger.Debug($"  Receive Only ............................ : { adapter.IsReceiveOnly}");
        //        Logger.Debug($"  Multicast ............................... : { adapter.SupportsMulticast}");
        //        //ShowInterfaceStatistics(adapter);

        //        Logger.Debug("");
        //    }
        //}
        //public static NetworkInterface GetNetworkInterface(string nicName)
        //{
        //    NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
        //    foreach (NetworkInterface nic in nics)
        //        if (nic.Name == nicName)
        //            return nic;

        //    return null;
        //}
        public static bool IsConnectedToInternet(string remoteHost)
        {
            bool result = false;
            Ping p = new Ping();
            try
            {
                PingReply reply = p.Send(remoteHost, 3000);
                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch { }
            return result;
        }
        #endregion

        public static bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                    stream.Close();
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }

            //file is not locked
            return false;
        }

        public static void BroadCastTime(int port)
        {
            //int PORT = 9876;
            UdpClient udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));

            var from = new IPEndPoint(0, 0);
            System.Threading.Tasks.Task.Run(() =>
            {
                while (!Configuration.GlobalShutDown)
                {
                    var recvBuffer = udpClient.Receive(ref from);
                    Logger.Debug(System.Text.Encoding.UTF8.GetString(recvBuffer));
                }
            });

            var data = System.Text.Encoding.UTF8.GetBytes("ABCD");
            udpClient.Send(data, data.Length, "255.255.255.255", port);
        }
        //public static T Json<T>(bool ser, Stream stream, T obj)
        //{
        //    T unpackedObject = default(T);

        //    // Creates serializer.
        //    var serializer = MsgPack.Serialization.MessagePackSerializer.Get<T>();

        //    try
        //    {
        //        if (ser)
        //        {
        //            // Pack obj to stream.
        //            serializer.Pack(stream, obj);
        //        }
        //        else
        //        {
        //            // Unpack from stream.
        //            unpackedObject = serializer.Unpack(stream);
        //        }
        //    }
        //    catch (Exception ee)
        //    {
        //        Logger.Log(ee);
        //    }

        //    return unpackedObject;
        //}

        static bool isLinux;
        static bool isWindows;
        static bool isRPi;
        internal static bool IsWindows { get { return isWindows; } set { isWindows = value; } }
        internal static bool IsRpi { get { return isRPi; } set { isRPi = value; } }
        public static bool IsLinux { get { return isLinux; } set { isLinux = value; } }
        public static string GetLinuxDistro()
        {
            if (IsWindows)
                throw new NotSupportedException("This MS-Windows machine!");

            string distro = string.Empty;
         
            return distro;
        }

        #region Settings
        /// <summary>
        /// Load settings.ini file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static Dictionary<string, string> LoadKeyValueSettings(string file)
        {
            if (!File.Exists(file)) return null;

            Dictionary<string, string> sd = new Dictionary<string, string>();
            List<string> lines = new List<string>();
            lines.AddRange(File.ReadAllLines(file));

            lines.ForEach(l =>
            {
                string l1 = l.Trim();
                if (!string.IsNullOrEmpty(l1))
                {
                    if (!l1.StartsWith("#"))
                    {
                        string[] chk = l1.Split(Defs.Splitter8, StringSplitOptions.RemoveEmptyEntries);
                        chk[0] = chk[0].Trim();
                        chk[1] = chk[1].Trim();
                        if (chk.Length == 2)
                            if (!sd.ContainsKey(chk[0]))
                                sd.Add(chk[0], chk[1]);
                    }
                }
            });

            return sd;
        }
        public static void SaveKeyValueSettings(Dictionary<string, string> sdic)
        {
            File.Delete(Configuration.GetConfigFilePath());

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (string k in sdic.Keys)
                sb.AppendFormat("{0} === {1}\n", k, sdic[k]);
            File.WriteAllText(Configuration.GetConfigFilePath(), sb.ToString());
        }
        #endregion

        public static event EventHandler FtpHandler;

        /// <summary>
        /// Downloads file from url and stores it in the Temp Dir.
        /// </summary>
        /// <param name="FtpFileURI"></param>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static (string path2DownloadedFile, string Err) DownloadFtpFile(string FtpFileURI, string filename)
        {
            Logger.Debug(string.Format("Request to download '{0}'. url='{1}'", filename, FtpFileURI));
            string err = "";
            string path = Configuration.GetTempDir() + filename;
            try
            {
                //NetworkCredential credentials = new NetworkCredential("username", "password");

                // Query size of the file to be downloaded
                WebRequest sizeRequest = WebRequest.Create(FtpFileURI);
                //WebResponse resp = sizeRequest.GetResponse();
                //sizeRequest.Credentials = credentials;
                sizeRequest.Method = WebRequestMethods.Ftp.GetFileSize;
                int size = (int)sizeRequest.GetResponse().ContentLength;

                FtpHandler?.Invoke(size, null);
                //Invoke((MethodInvoker)(() => progressBar1.Maximum = size));
                // Download the file
                WebRequest request = WebRequest.Create(FtpFileURI);
                //request.Credentials = credentials;
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                using (Stream ftpStream = request.GetResponse().GetResponseStream())
                using (Stream fileStream = File.Create(path))
                {
                    byte[] buffer = new byte[10240];
                    int read;
                    while ((read = ftpStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fileStream.Write(buffer, 0, read);
                        int position = (int)fileStream.Position;
                        //Invoke((MethodInvoker)(() => progressBar1.Value = position));

                        FtpHandler?.Invoke(position, null);
                    }
                }
            }
            catch (WebException we)
            {
                err = string.Format("{0}({1})", we.Message, we.Status);
                path = string.Empty;
            }
            catch (Exception e)
            {
                err = e.Message;
                Logger.Debug(e.ToString());
                path = string.Empty;
            }

            return (path, err);
        }

        //public static string DownloadFtpFile2(FtpAction ftpAct, string serverIP)
        //{
        //    string file = ftpAct == FtpAction.DownloadSettings ? Defs.ConfigurationFile : Defs.AppFile;
        //    string url = string.Format("ftp://{0}/{1}", serverIP, file);

        //    string path2File = string.Empty;

        //    try
        //    {
        //        var ret = Util.DownloadFtpFile(url, file);
        //        path2File = ret.path2File;
        //        Logger.Debug(string.IsNullOrEmpty(path2File) ? "Download failed! Reason: " + ret.Err : "Download finished successfully!");
        //    }
        //    catch (Exception ee) { Logger.Log(ee); }

        //    return path2File;
        //}
        public static void CreateNecessaryFolders()
        {
            Directory.CreateDirectory(Configuration.GetBackupDir());
            Directory.CreateDirectory(Configuration.GetConfDir());
            Directory.CreateDirectory(Configuration.GetDataDir());
            Directory.CreateDirectory(Configuration.GetImageDir());
            Directory.CreateDirectory(Configuration.GetLogDir());
            Directory.CreateDirectory(Configuration.GetLogBackupDir());
            Directory.CreateDirectory(Configuration.GetScriptDir());
            Directory.CreateDirectory(Configuration.GetSessionsDataDir());
            Directory.CreateDirectory(Configuration.GetTempDir());
            Directory.CreateDirectory(Configuration.GetToolsDir());

            SystemUtility.CreateTempFolder();
        }

        public static void CreateRamDisk()
        {
            if (IsLinux)
            {
                //https://www.linuxbabe.com/command-line/create-ramdisk-linux
                string dir = Configuration.GetRamDiskDir();
                Directory.CreateDirectory(dir);
                string mnt = $"-c \"sudo mount -t tmpfs -o size=1M {Defs.Dir_RamDiskMountName} {dir}\"";
                Process.Start("/bin/bash", mnt);
                ///sudo mount -t tmpfs -o size=1024m myramdisk /tmp/ramdisk
                ///sudo mount -t tmpfs -o size=10G myramdisk /tmp/ramdisk
                ///sudo mount -t tmpfs -o size=2G tmpfs /mnt/ramdisk
            }
            else if (isWindows)
            {
                //string dir = Configuration.GetRamDiskDir();
                //Console.WriteLine(dir);
                //Directory.CreateDirectory(dir);
                //string mnt = $"-c \"sudo mount -t tmpfs -o size=1M {Defs.Dir_RamDiskMountName} {dir}\"";
                //Process.Start("/bin/bash", mnt);
            }
        }
    }


    public class CommandLineArgs
    {
        public const string InvalidSwitchIdentifier = "INVALID";
        List<string> prefixRegexPatternList = new List<string>();
        Dictionary<string, string> arguments = new Dictionary<string, string>();
        List<string> invalidArgs = new List<string>();
        Dictionary<string, EventHandler<CommandLineArgsMatchEventArgs>> handlers = new Dictionary<string, EventHandler<CommandLineArgsMatchEventArgs>>();
        //bool ignoreCase = true;

        public event EventHandler<CommandLineArgsMatchEventArgs> SwitchMatch;

        public int ArgCount { get { return arguments.Keys.Count; } }

        public List<string> PrefixRegexPatternList
        {
            get { return prefixRegexPatternList; }
        }

        public bool IgnoreCase
        {
            get;// { return ignoreCase; }
            set;// { ignoreCase = value; }
        }

        public string[] InvalidArgs
        {
            get { return invalidArgs.ToArray(); }
        }

        public string this[string key]
        {
            get
            {
                if (ContainsSwitch(key)) return arguments[key];
                return null;
            }
        }

        protected virtual void OnSwitchMatch(CommandLineArgsMatchEventArgs e)
        {
            if (handlers.ContainsKey(e.Switch) && handlers[e.Switch] != null) handlers[e.Switch](this, e);
            else if (SwitchMatch != null) SwitchMatch(this, e);
        }

        public void RegisterSpecificSwitchMatchHandler(string switchName, EventHandler<CommandLineArgsMatchEventArgs> handler)
        {
            if (handlers.ContainsKey(switchName)) handlers[switchName] = handler;
            else handlers.Add(switchName, handler);
        }

        public bool ContainsSwitch(string switchName)
        {
            foreach (string pattern in prefixRegexPatternList)
            {
                if (Regex.IsMatch(switchName, pattern, RegexOptions.Compiled))
                {
                    switchName = Regex.Replace(switchName, pattern, "", RegexOptions.Compiled);
                }
            }
            if (IgnoreCase)
            {
                foreach (string key in arguments.Keys)
                {
                    if (key.ToLower() == switchName.ToLower()) return true;
                }
            }
            else
            {
                return arguments.ContainsKey(switchName);
            }
            return false;
        }

        public void ProcessCommandLineArgs(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string value = IgnoreCase ? args[i].ToLower() : args[i];
                foreach (string prefix in prefixRegexPatternList)
                {
                    string pattern = string.Format("^{0}", prefix);
                    if (Regex.IsMatch(value, pattern, RegexOptions.Compiled))
                    {
                        value = Regex.Replace(value, pattern, "", RegexOptions.Compiled);
                        if (value.Contains("="))
                        { // "<prefix>Param=Value"
                            int idx = value.IndexOf('=');
                            string n = value.Substring(0, idx);
                            string v = value.Substring(idx + 1, value.Length - n.Length - 1);
                            OnSwitchMatch(new CommandLineArgsMatchEventArgs(n, v));
                            arguments.Add(n, v);
                        }
                        else
                        { // "<prefix>Param Value"
                            if (i + 1 < args.Length)
                            {
                                string @switch = value;
                                string val = args[i + 1];
                                OnSwitchMatch(new CommandLineArgsMatchEventArgs(@switch, val));
                                arguments.Add(value, val);
                                i++;
                            }
                            else
                            {
                                OnSwitchMatch(new CommandLineArgsMatchEventArgs(value, null));
                                arguments.Add(value, null);
                            }
                        }
                    }
                    else
                    { // invalid arg ...
                        OnSwitchMatch(new CommandLineArgsMatchEventArgs(InvalidSwitchIdentifier, value, false));
                        invalidArgs.Add(value);
                    }
                }
            }
        }
    }
    public class CommandLineArgsMatchEventArgs : EventArgs
    {
        string @switch;
        string value;
        bool isValidSwitch = true;

        public string Switch
        {
            get { return @switch; }
        }

        public string Value
        {
            get { return value; }
        }

        public bool IsValidSwitch
        {
            get { return isValidSwitch; }
        }

        public CommandLineArgsMatchEventArgs(string @switch, string value)
            : this(@switch, value, true) { }

        public CommandLineArgsMatchEventArgs(string @switch, string value, bool isValidSwitch)
        {
            this.@switch = @switch;
            this.value = value;
            this.isValidSwitch = isValidSwitch;
        }
    }
    public class Crc16
    {
        public enum Crc16Mode : ushort { Standard = 0xA001, CcittKermit = 0x8408 }
        readonly ushort[] table = new ushort[256];

        public ushort ComputeChecksum(params byte[] bytes)
        {
            ushort crc = 0;
            for (int i = 0; i < bytes.Length; ++i)
            {
                byte index = (byte)(crc ^ bytes[i]);
                crc = (ushort)((crc >> 8) ^ table[index]);
            }
            return crc;
        }

        public byte[] ComputeChecksumBytes(params byte[] bytes)
        {
            ushort crc = ComputeChecksum(bytes);
            return BitConverter.GetBytes(crc);
        }

        public Crc16(Crc16Mode mode)
        {
            ushort polynomial = (ushort)mode;
            ushort value;
            ushort temp;
            for (ushort i = 0; i < table.Length; ++i)
            {
                value = 0;
                temp = i;
                for (byte j = 0; j < 8; ++j)
                {
                    if (((value ^ temp) & 0x0001) != 0)
                    {
                        value = (ushort)((value >> 1) ^ polynomial);
                    }
                    else
                    {
                        value >>= 1;
                    }
                    temp >>= 1;
                }
                table[i] = value;
            }
        }
    }
    public static class Crc8
    {
        static byte[] table = new byte[256];
        // x8 + x7 + x6 + x4 + x2 + 1
        const byte poly = 0xd5;

        public static byte ComputeChecksum(params byte[] bytes)
        {
            byte crc = 0;
            if (bytes != null && bytes.Length > 0)
            {
                foreach (byte b in bytes)
                {
                    crc = table[crc ^ b];
                }
            }
            return crc;
        }

        static Crc8()
        {
            for (int i = 0; i < 256; ++i)
            {
                int temp = i;
                for (int j = 0; j < 8; ++j)
                {
                    if ((temp & 0x80) != 0)
                    {
                        temp = (temp << 1) ^ poly;
                    }
                    else
                    {
                        temp <<= 1;
                    }
                }
                table[i] = (byte)temp;
            }
        }
    }
}

