using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace lab2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            try
            {
                new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp).Close();
            }
            catch
            {
                Console.WriteLine("Ошибка: требуется запуск от имени администратора!");
                return;
            }

            Console.Write("Введите IP-адрес: ");
            string ipAddress = Console.ReadLine().Trim();

            if (!IPAddress.TryParse(ipAddress, out IPAddress targetIP))
            {
                Console.WriteLine("Ошибка: некорректный IP-адрес");
                return;
            }

            Console.WriteLine($"\nТрассировка маршрута к {ipAddress}\n");

            ushort pid = (ushort)Process.GetCurrentProcess().Id;
            bool targetReached = false;

            for (int ttl = 1; ttl <= 30 && !targetReached; ttl++)
            {
                Console.Write($"{ttl,2}");
                IPAddress hopAddress = null;

                for (int i = 0; i < 3; i++)
                {
                    ushort seq = (ushort)(ttl * 3 + i);
                    long time = -1;
                    IPAddress responder = null;

                    try
                    {
                        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp))
                        {
                            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);
                            socket.ReceiveTimeout = 3000;

                            byte[] packet = new byte[40];
                            packet[0] = 8;
                            packet[1] = 0;

                            packet[4] = (byte)(pid >> 8);
                            packet[5] = (byte)(pid & 0xFF);

                            packet[6] = (byte)(seq >> 8);
                            packet[7] = (byte)(seq & 0xFF);

                            byte[] timeBytes = BitConverter.GetBytes(DateTime.Now.Ticks);
                            Array.Copy(timeBytes, 0, packet, 8, Math.Min(8, packet.Length - 8));

                            int sum = 0;
                            for (int j = 0; j < packet.Length; j += 2)
                            {
                                sum += (packet[j] << 8) | (j + 1 < packet.Length ? packet[j + 1] : 0);
                            }
                            while ((sum >> 16) > 0) sum = (sum & 0xFFFF) + (sum >> 16);
                            ushort checksum = (ushort)(~sum);

                            packet[2] = (byte)(checksum >> 8);
                            packet[3] = (byte)(checksum & 0xFF);

                            Stopwatch sw = Stopwatch.StartNew();
                            socket.SendTo(packet, new IPEndPoint(IPAddress.Parse(ipAddress), 0));

                            byte[] buffer = new byte[1024];
                            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                            int received = socket.ReceiveFrom(buffer, ref remoteEP);
                            sw.Stop();

                            int ipHeaderLen = (buffer[0] & 0x0F) * 4;
                            byte icmpType = buffer[ipHeaderLen];

                            if (icmpType == 11 || icmpType == 0)
                            {
                                responder = ((IPEndPoint)remoteEP).Address;
                                time = sw.ElapsedMilliseconds;

                                if (responder.Equals(targetIP)) targetReached = true;
                            }
                        }
                    }
                    catch (SocketException) { }

                    if (responder != null)
                    {
                        if (hopAddress == null)
                        {
                            hopAddress = responder;
                            Console.Write($"    {responder,-15}");
                        }
                        Console.Write($"  {time,4}ms");
                    }
                    else
                    {
                        if (i == 0) Console.Write("    *");
                        Console.Write("     *");
                    }

                    Thread.Sleep(100);
                }
                Console.WriteLine();
            }

            if (targetReached) Console.WriteLine($"\nЦелевой узел {ipAddress} достигнут.");
            else Console.WriteLine("\nПревышен лимит прыжков.");
        }
    }
}