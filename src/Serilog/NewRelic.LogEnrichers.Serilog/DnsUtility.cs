using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NewRelic.LogEnrichers
{
    public interface IDnsUtility
    {
        string GetHostName();
        IPAddress GetLocalIPAddress();
        string GetFullHostName();
        string GetDnsSuffix();
    }

    public class DnsUtility : IDnsUtility
    {
        public string GetHostName()
        {
            return Dns.GetHostName();
        }

        public string GetFullHostName()
        {
            return $"{GetHostName()}.{GetDnsSuffix()}";
        }

        public IPAddress GetLocalIPAddress()
        {
            // connect to NR to get active interface, 0 means use next open port, Java does the same
            using (var udpClient = new UdpClient("newrelic.com", 0))
            {
                return ((IPEndPoint)udpClient.Client.LocalEndPoint).Address;
            }
        }

        private NetworkInterface GetActiveNetworkInterface(IPAddress localIPAddress)
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in networkInterfaces)
            {
                foreach (var ipAddress in networkInterface.GetIPProperties().UnicastAddresses)
                {

                    if (localIPAddress == ipAddress.Address)
                    {
                        return networkInterface;
                    }
                }
            }

            return null;
        }

        public string GetDnsSuffix()
        {
            var ipAddress = GetLocalIPAddress();
            var networkInterface = GetActiveNetworkInterface(ipAddress);

            return networkInterface?.GetIPProperties()?.DnsSuffix;
        }
    }


    public interface INewRelicService
    {
        string GetEntityID(string hostName, string serviceName);
    }

    public class NewRelicService : INewRelicService
    {
        private const string _endpointUrl = "https://nerd-graph.service.newrelic.com/graphql";
        private readonly string _apiKey;

        public NewRelicService(string apiKey)
        {
            apiKey = _apiKey;
        }

        public string GetEntityID(string hostName, string serviceName)
        {
            return "1234456";
        }
    }
}
