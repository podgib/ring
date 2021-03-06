﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Kerv.Common
{
    public class StatementHandler : AuthenticatedRequestHandler
    {
        public class InvalidFormatException : Exception {}

        static readonly String statementUrl =
            "https://kerv.com/en/account/statement/";
        static readonly String transactionsUrl = 
            "https://kerv.com/umbraco/Surface/KervAccount/CardStatementGet?page=1&duration=0&deviceid={0}&culture=en-GB";

        private List<Transaction> transactions;
        public List<Transaction> Transactions { get => transactions; }
        public List<Device> Devices { get; }

        public StatementHandler(LoggedOutListener listener) : base(listener) {
            transactions = new List<Transaction>();
            Devices = new List<Device>();
        }

        public async Task<bool> RefreshStatement()
        {
            try
            {
                var response = await Get(statementUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }
                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                var stream = await response.Content.ReadAsStreamAsync();
                doc.Load(stream);
                var balance = ParseBalance(doc);
                ParseTransactions(doc);
                ParseDevices(doc);
                Account.Balance = balance;
            } catch(InvalidFormatException) {
                return false;
            } catch (WebException) {
                return false;
            } catch (Exception e) {
                return false;
            } 
            return true;
        }

        public async Task<bool> TransactionsForDevice(Device device) {
            try
            {
                var response = await Get(String.Format(transactionsUrl, device.ID));
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }
                var doc = new HtmlDocument();
                var stream = await response.Content.ReadAsStreamAsync();
                doc.Load(stream);
                ParseTransactions(doc);
            } catch (Exception) {
                return false;
            }
            return true;
        }

        private Money ParseBalance(HtmlDocument html)
        {
            var spans = html.DocumentNode.Descendants("span");
            foreach (var span in spans)
            {
                if (span.Attributes.Contains("class") &&
                    span.Attributes["class"].Value
                        .Contains("accountstatus__amount"))
                {
                    return new Money(span.InnerText);
                }
            }
            throw new InvalidFormatException();
        }

        private void ParseTransactions(HtmlDocument html)
        {
            var table = html.GetElementbyId("transactions");
            var rows = table.Descendants("tr");
            transactions = new List<Transaction>();
            foreach (var row in rows)
            {
                if (row.ChildNodes[1].Name == "th")
                {
                    continue;
                }
                var cells = row.ChildNodes;
                var dateString = cells[1].InnerText.Trim();
                var deviceString = cells[3].InnerText.Trim();
                var description = cells[5].InnerText.Trim();
                var amountString = cells[7].InnerText.Trim();
                var date = DateTime.Parse(dateString);
                var amount = new Money(amountString);
                Transactions.Add(new Transaction { Date = date, 
                    Amount = amount, 
                    Description = description, 
                    Device = deviceString 
                });
            }
        }

        private void ParseDevices(HtmlDocument html) {
            var select = html.GetElementbyId("deviceID");
            var options = select.Descendants("option");
            Devices.Clear();
            foreach (var option in options) {
                var deviceName = option.NextSibling.InnerText.Trim();
                var deviceId = long.Parse(option.Attributes["value"].Value);
                if (deviceId != 0) {
                    Devices.Add(new Device(deviceName, deviceId));
                }
            }
        }
    }
}
