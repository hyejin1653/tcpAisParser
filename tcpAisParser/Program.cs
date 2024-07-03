using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace tcpAisParser
{
    internal class Program
    {
        public static ConcurrentQueue<String> q = new ConcurrentQueue<string>();
        // public static UdpClient srv;
        //public static IPEndPoint remoteEP;

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        static void Main(string[] args)
        {

            StringBuilder tcp_ip = new StringBuilder();
            StringBuilder tcp_port = new StringBuilder();
            StringBuilder udp_ip = new StringBuilder();
            StringBuilder udp_port = new StringBuilder();

            GetPrivateProfileString("SETTING", "TCP_IP", "(NONE)", tcp_ip, 32, System.IO.Directory.GetCurrentDirectory() + @"\config.ini");
            GetPrivateProfileString("SETTING", "TCP_PORT", "(NONE)", tcp_port, 32, System.IO.Directory.GetCurrentDirectory() + @"\config.ini");
            GetPrivateProfileString("SETTING", "UDP_IP", "(NONE)", udp_ip, 32, System.IO.Directory.GetCurrentDirectory() + @"\config.ini");
            GetPrivateProfileString("SETTING", "UDP_PORT", "(NONE)", udp_port, 32, System.IO.Directory.GetCurrentDirectory() + @"\config.ini");

            string tcpIp = tcp_ip.ToString();
            int tcpPort = Convert.ToInt32(tcp_port.ToString());
            string udpIp = udp_ip.ToString();
            int udpPort = Convert.ToInt32(udp_port.ToString());

            Program pg = new Program();

            UdpClient cli = new UdpClient();
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            Task tDeq = Task.Factory.StartNew(() =>
            {
                string result;
                while (true)
                {
                    if (q.TryDequeue(out result))
                    {
                        byte[] message = pg.ProcessMessage(result);
                        if(message != null && message.Length > 0)
                        {
                            cli.Send(message, message.Length, udpIp, udpPort);
                        }
                        
                    }
                        //pg.ProcessMessage(result);

                    //Thread.Sleep(500);
                }
            });

            try
            {
                string strRecvMsg = "";

                TcpClient sockClient = new TcpClient(tcpIp, tcpPort);
                NetworkStream ns = sockClient.GetStream();
                StreamReader sr = new StreamReader(ns);
                StreamWriter sw = new StreamWriter(ns);

                while (true)
                {
                    strRecvMsg = sr.ReadLine();
                    if (strRecvMsg == null)
                    {
                        break;
                    }
                    q.Enqueue(strRecvMsg);
                    //Console.WriteLine(strRecvMsg);
                }

                sr.Close();
                sw.Close();
                ns.Close();
                sockClient.Close();

                //Console.WriteLine("접속 종료");
                Console.ReadLine();
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.ToString());
            }

            
        }

        public String complete = "";
        public String strAscllStatus = "";

        byte[] ProcessMessage(String str)
        {
            string[] msg = str.Split(',');

            if (msg.Length <= 5) return null;

            int flg = Convert.ToInt32(msg[1].ToString()); // 1: 동적, 2: 정적

            string mainStr = msg[5].ToString();

            if (flg == 1) //동적정보일 경우
            {
                string strAscll = "";

                for (int i = 0; i < mainStr.Length; i++)
                {
                    string strA = mainStr[i].ToString();
                    char strCh = Convert.ToChar(strA);
                    int ascInt = Convert.ToInt32(strCh) - 48;

                    if (ascInt >= 40)
                    {
                        ascInt = ascInt - 8;
                    }

                    string ascStr = Convert.ToString(ascInt, 2).PadLeft(6, '0').ToString();

                    strAscll = strAscll + ascStr;
                }

                int messageType = Convert.ToInt32(strAscll.Substring(0, 6), 2);

                //동적정보
                if (messageType == 1 || messageType == 2 || messageType == 3)
                {
                    int changeInt = 0;
                    double doInt = 0;

                    string mmsi = Convert.ToInt32(strAscll.Substring(8, 30), 2).ToString();

                    string rot = strAscll.Substring(42, 8);
                    changeInt = Convert.ToInt32(rot.ToString(), 2);
                    rot = string.Format("{0:0.000000}", changeInt);

                    string lat = strAscll.Substring(61, 28);
                    changeInt = Convert.ToInt32(lat.ToString(), 2);
                    string latStr = changeInt.ToString();
                    doInt = changeInt / 600000.0;
                    lat = string.Format("{0:0.000000}", doInt);

                    string lon = strAscll.Substring(89, 27);
                    changeInt = Convert.ToInt32(lon.ToString(), 2);
                    string lonStr = changeInt.ToString();
                    doInt = changeInt / 600000.0;
                    lon = string.Format("{0:0.000000}", doInt);

                    string sog = strAscll.Substring(50, 10);
                    changeInt = Convert.ToInt32(sog.ToString(), 2);
                    sog = string.Format("{0:0.000000}", changeInt);

                    string cog = strAscll.Substring(116, 12);
                    changeInt = Convert.ToInt32(cog.ToString(), 2);
                    doInt = changeInt * 0.1;
                    cog = string.Format("{0:0.000000}", doInt);

                    string heading = strAscll.Substring(128, 9);
                    changeInt = Convert.ToInt32(heading.ToString(), 2);
                    doInt = changeInt * 0.1;
                    heading = string.Format("{0:0.000000}", doInt);

                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");

                    string message = mmsi + "," + lat + "," + lon + "," + sog + "," + rot + "," + cog + "," + heading + "," + timestamp;
                    byte[] bytes = Encoding.ASCII.GetBytes(message);

                    Console.WriteLine(mmsi + "," + lat + "," + lon + "," + sog + "," + rot + "," + cog + "," + heading + "," + timestamp);


                    return bytes;
                }
                //return;
            }
            else //정적정보
            {
                int rowCont = Convert.ToInt32(msg[2].ToString());

                if (rowCont == 1) strAscllStatus = "";

                for (int i = 0; i < mainStr.Length; i++)
                {
                    string strA = mainStr[i].ToString();
                    char strCh = Convert.ToChar(strA);
                    int ascInt = Convert.ToInt32(strCh) - 48;

                    if (ascInt >= 40)
                    {
                        ascInt = ascInt - 8;
                    }

                    string ascStr = Convert.ToString(ascInt, 2).PadLeft(6, '0').ToString();
                    strAscllStatus = strAscllStatus + ascStr;
                }

                int messageType = Convert.ToInt32(strAscllStatus.Substring(0, 6), 2);

                //정적이고 lineNo가 2인경우에 발생하게 만든다.
                if (messageType == 5 && rowCont == 1) return null;

                //정적정보
                if (messageType == 5)
                {
                    string mmsi = Convert.ToInt32(strAscllStatus.Substring(8, 30), 2).ToString();
                    string shipName = strAscllStatus.Substring(112, 120);

                    int nbytes = shipName.Length / 6;
                    byte[] outBytes = new byte[nbytes];
                    for (int i = 0; i < nbytes; i++)
                    {
                        string binStr = shipName.Substring(i * 6, 6);
                        int cnt = Convert.ToInt32(binStr, 2) + 64;
                        if (cnt > 64 && cnt <= 95)
                        {
                            outBytes[i] = Convert.ToByte(Convert.ToInt32(binStr, 2) + 64);
                        }
                        else
                        {
                            outBytes[i] = Convert.ToByte(Convert.ToInt32(binStr, 2));
                        }
                    }
                    shipName = Encoding.UTF8.GetString(outBytes);

                    string shipType = Convert.ToInt32(strAscllStatus.Substring(232, 8), 2).ToString();

                    Console.WriteLine(" 정적 : " + mmsi + "," + shipName + "," + shipType);

                    string message = mmsi + "," + shipName + "," + shipType;
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    return bytes;
                }
            }

            return null;
        }
    
    }

}
