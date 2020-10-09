using NUnit.Framework;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using static Test.TestHelper;
using static System.TimeSpan;
using System.Security.Cryptography.X509Certificates;

namespace Test
{
    [TestFixture]
    [NonParallelizable]
    [Category("SendPacket")]
    public class SendPacketTest
    {
        private const string Filter = "ether proto 0x1234";

        [Test]
        public void TestSendPacketTest()
        {
            var packet = EthernetPacket.RandomPacket();
            packet.Type = (EthernetType)0x1234;
            var received = RunCapture(Filter, (device) =>
            {
                device.SendPacket(packet);
            });
            Assert.That(received, Has.Count.EqualTo(1));
            CollectionAssert.AreEquivalent(packet.Bytes, received[0].Data);
        }

        [Test]
        public void TestSendArpPacketTest()
        {
            var localIPBytes = new byte[] { 192, 168, 1, 170 };
            var localIP = new System.Net.IPAddress(localIPBytes);

            var destinationIPBytes = new byte[] { 192, 168, 1, 1 };
            var destinationIP = new System.Net.IPAddress(destinationIPBytes);
            var adapters = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var f = adapters.First(x => x.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
             && x.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet);
            var adapterMac = f.GetPhysicalAddress();
            var adapterIP = f.GetIPProperties().UnicastAddresses.First(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            var props = f.GetIPProperties();
            var gateway = props.GatewayAddresses.First();
            var localMac = System.Net.NetworkInformation.PhysicalAddress.Parse("AA-BB-CC-DD-EE-FF");

            localIP = adapterIP.Address;
            localMac = adapterMac;
            destinationIP = gateway.Address;
            var broadcastMac = System.Net.NetworkInformation.PhysicalAddress.Parse("00-00-00-00-00-00");
            var arpRequestPacket = new ArpPacket(ArpOperation.Request,
                                  broadcastMac,
                                  destinationIP,
                                  localMac,
                                  localIP);


            //packet.Type = (EthernetType)0x1234;
            string filter = "arp";
            var device = GetPcapDevice();
            device.Open();
            device.SendPacket(arpRequestPacket);
            device.Filter = filter;
            var received = new List<RawCapture>();
            device.SendPacket(arpRequestPacket);

           

            RawCapture query = null;
            while ((query = device.GetNextPacket()) == null)
            {
                System.Threading.Thread.Sleep(100);
            }
            received.Add(query);

            RawCapture response = device.GetNextPacket();
            while ((response = device.GetNextPacket()) == null)
            {
                System.Threading.Thread.Sleep(100);
            }
            received.Add(response);


            device.Close();

            var packets = received
                .Select(raw => raw.GetPacket().Extract<ArpPacket>())
                .Where(x => x != null)
                .ToList();

            var capturedQuery = packets.FirstOrDefault(x =>
                  x.SenderProtocolAddress.ToString() == localIP.ToString()
                  && x.TargetProtocolAddress.ToString() == destinationIP.ToString());

            var arpResponsePacket = packets.FirstOrDefault(x =>
                    x.SenderProtocolAddress.ToString() == destinationIP.ToString()
                    && x.TargetProtocolAddress.ToString() == localIP.ToString());
            Assert.IsNotNull(arpResponsePacket);

            Assert.AreEqual(capturedQuery.TargetHardwareAddress, broadcastMac);
            Assert.AreNotEqual(arpResponsePacket.TargetHardwareAddress, broadcastMac);
        }

        //Packet parseCapture(RawCapture rawCapture)
        //{
        //    var p = Packet.ParsePacket(rawCapture.GetLinkLayers(), rawCapture.Data);
        //    return p;
        //}

        [SetUp]
        public void SetUp()
        {
            TestHelper.ConfirmIdleState();
        }

        [TearDown]
        public void Cleanup()
        {
            TestHelper.ConfirmIdleState();
        }
    }
}
