﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using CryptoExchanges;
using System;
using System.Collections.Generic;

namespace CryptoExchanges.Tests.Exchanges
{
  [TestClass()]
  public class BinanceTests : ExchangeMonitorTests
  {
    [TestMethod()]
    public void Binance()
    {
      ExchangeMonitorConfig config = new ExchangeMonitorConfig(ExchangeName.Binance);
      monitor = new ExchangeMonitor(config);
      Assert.IsTrue(Coin.ethereum.Best(Coin.bitcoin, true).askPrice > 0);
    }
  }
}