﻿using System;
using System.Collections.Generic;
using System.Media;
using System.Timers;
using System.Linq;
using static CrypConnect.GoogleSheetsExamples.AboutCoin;
using System.Threading.Tasks;

namespace CrypConnect.GoogleSheetsExamples
{
  /// <summary>
  /// TODO
  ///  - Add buy targets
  ///  - Add total 24-volume seen (and total from coinmarketcap)
  ///     - or maybe the max of the two?
  /// </summary>
  public class GoogleSheetPriceMonitor
  {
    const string dataDumpTab = "CrypConnect";
    const string settingsTab = "CrypConnectSettings";
    const string arbAlarmTab = "ArbAlarm";

    // TODO USD goal
    const decimal arbPurchasePriceETH = 1m;
    const decimal arbPurchasePriceBTC = .1m;

    readonly GoogleSheet sheet;

    readonly Timer refreshTimer;

    ExchangeMonitor exchangeMonitor;

    public GoogleSheetPriceMonitor()
    {
      //sheet = new GoogleSheet("1e6jCpqkQEGwICemF0iVzRDBQwmNUskpZoT5nY3SRd24");
      sheet = new GoogleSheet("17ajUh3JI6J-VeLfdyPl9JhvoYIz_NhTWCoeJRYY8GQ8");

      refreshTimer = new Timer()
      {
        AutoReset = false,
        Interval = TimeSpan.FromSeconds(10).TotalMilliseconds
      };
      refreshTimer.Elapsed += RefreshTimer_Elapsed;
    }

    public async Task Start()
    {
      ExchangeMonitorConfig config = new ExchangeMonitorConfig(
        ExchangeName.Binance,
        ExchangeName.Cryptopia,
        ExchangeName.Kucoin,
        //ExchangeName.AEX,
        ExchangeName.GDax,
        ExchangeName.Idex
        );


      IList<IList<object>> settings = await sheet.Read(settingsTab, "A:A");
      List<string> blacklist = new List<string>();
      foreach (var row in settings)
      {
        object blacklistData = row.FirstOrDefault();
        if (blacklistData == null)
        {
          continue;
        }
        blacklist.Add((string)blacklistData);
      }

      config.BlacklistCoins(blacklist.ToArray());

      exchangeMonitor = new ExchangeMonitor(config);

      RefreshTimer_Elapsed(null, null);
    }

    async void RefreshTimer_Elapsed(
      object sender,
      ElapsedEventArgs e)
    {
      await DumpAllCoinsAndStats();
      await RefreshArb();
      await ConsiderAlarming();

      Console.Write(".");

      refreshTimer.Start();
    }

    async Task DumpAllCoinsAndStats()
    {
      List<Coin> allCoinList = exchangeMonitor.allCoins.ToList();

      List<string[]> results = new List<string[]>();
      AboutCoin headers = new AboutCoin();
      headers.PopulateWithColumnNames();
      results.Add(headers.ToArray());

      for (int i = 0; i < allCoinList.Count; i++)
      {
        AboutCoin about = DescribeCoin(allCoinList[i].fullName);
        if (about != null)
        {
          results.Add(about.ToArray());
        }
      }

      for (int i = 0; i < 100; i++)
      { // Clear some old coins
        results.Add(new AboutCoin().ToArray());
      }

      await sheet.Write(dataDumpTab, "A1", results);
    }

    async Task RefreshArb()
    {
      IList<IList<object>> results = await sheet.Read(arbAlarmTab, "B:K");

      List<string[]> dataToWrite = new List<string[]>(results.Count);

      TradingPair bestBtcUsdAsk = Coin.bitcoin.Best(Coin.usd, false);
      TradingPair bestBtcUsdBid = Coin.bitcoin.Best(Coin.usd, true);
      TradingPair bestEthUsdAsk = Coin.ethereum.Best(Coin.usd, false);
      TradingPair bestEthUsdBid = Coin.ethereum.Best(Coin.usd, true);

      for (int iRow = 0; iRow < results.Count; iRow++)
      {
        dataToWrite.Add(new string[2]
        {
          "",""
        });

        IList<object> row = results[iRow];
        if (row[9]?.ToString() == "TRUE")
        {
          string coinName = row[0].ToString();
          Coin quoteCoin = Coin.FromName(coinName);
          string bidExchangeName = row[2].ToString();
          ExchangeName bidExchange = (ExchangeName)Enum.Parse(typeof(ExchangeName),
            bidExchangeName);
          string bidCurrencyName = row[3].ToString();
          Coin bidBaseCoin = Coin.FromName(bidCurrencyName);

          string askExchangeName = row[5].ToString();
          ExchangeName askExchange = (ExchangeName)Enum.Parse(typeof(ExchangeName),
            askExchangeName);
          string askCurrencyName = row[6].ToString();
          Coin askBaseCoin = Coin.FromName(askCurrencyName);

          decimal purchasePriceInBase;
          if (askBaseCoin == Coin.ethereum)
          {
            purchasePriceInBase = arbPurchasePriceETH;
          }
          else
          {
            purchasePriceInBase = arbPurchasePriceBTC;
          }

          (decimal purchaseAmount, decimal quantity) = await quoteCoin.CalcPurchasePrice(
            askExchange, askBaseCoin,
            purchasePriceInBase: purchasePriceInBase);
          if (askBaseCoin == Coin.bitcoin)
          {
            purchaseAmount *= bestBtcUsdAsk.askPrice;
          }
          else
          {
            purchaseAmount *= bestEthUsdAsk.askPrice;
          }

          decimal sellAmount = await quoteCoin.CalcSellPrice(
            bidExchange, bidBaseCoin,
            quantityOfCoin: quantity * 2);
          sellAmount /= 2;
          if (bidBaseCoin == Coin.bitcoin)
          {
            sellAmount *= bestBtcUsdBid.bidPrice;
          }
          else
          {
            sellAmount *= bestEthUsdBid.bidPrice;
          }

          dataToWrite[iRow][0] = purchaseAmount.ToString();
          dataToWrite[iRow][1] = sellAmount.ToString();
        }
      }


      await sheet.Write(arbAlarmTab, "L:M", dataToWrite);
    }

    async Task ConsiderAlarming()
    {
      IList<IList<object>> alarmData = await sheet.Read(settingsTab, "B2");
      if (alarmData.Count > 0
        && alarmData[0].Count > 0
        && alarmData[0][0] != null)
      {
        if (int.TryParse(alarmData[0][0].ToString(), out int alarmCount))
        {
          if (alarmCount > 0)
          {
            SoundPlayer player = new SoundPlayer(
            @"d:\\StreamAssets\\HeyLISTEN.wav");
            player.Play();
          }
        }
      }
    }

    AboutCoin DescribeCoin(
      object coinName)
    {
      AboutCoin about = new AboutCoin();

      Coin coin = Coin.FromName((string)coinName);
      if (coin == null)
      {
        return about;
      }

      if (coin.hasValidTradingPairs == false) //&& coin.coinMarketCapData == null)
      {
        return null;
      }

      about.columns[(int)Column.CoinName] = coin.fullName;

      TradingPair bestBtcBid = coin.Best(Coin.bitcoin, true);
      if (bestBtcBid != null)
      {
        about.columns[(int)Column.BestBidBTC] = bestBtcBid.bidPrice.ToString();
        about.columns[(int)Column.BestBidBTCExchange] = bestBtcBid.exchange.exchangeName.ToString();

        TradingPair bestBtcUsdBid = Coin.bitcoin.Best(Coin.usd, true);
        if (bestBtcUsdBid != null)
        {
          about.columns[(int)Column.BestBidBTCUSD] = (bestBtcUsdBid.bidPrice * bestBtcBid.bidPrice).ToString();
        }
      }
      TradingPair bestBtcAsk = coin.Best(Coin.bitcoin, false);
      if (bestBtcAsk != null)
      {
        about.columns[(int)Column.BestAskBTC] = bestBtcAsk.askPrice.ToString();
        about.columns[(int)Column.BestAskBTCExchange] = bestBtcAsk.exchange.exchangeName.ToString();

        TradingPair bestBtcUsdAsk = Coin.bitcoin.Best(Coin.usd, false);
        if (bestBtcUsdAsk != null)
        {
          about.columns[(int)Column.BestAskBTCUSD] = (bestBtcUsdAsk.askPrice * bestBtcAsk.askPrice).ToString();
        }
      }
      TradingPair bestEthBid = coin.Best(Coin.ethereum, true);
      if (bestEthBid != null)
      {
        about.columns[(int)Column.BestBidETH] = bestEthBid.bidPrice.ToString();
        about.columns[(int)Column.BestBidETHExchange] = bestEthBid.exchange.exchangeName.ToString();

        TradingPair bestEthUsdBid = Coin.ethereum.Best(Coin.usd, true);
        if (bestEthUsdBid != null)
        {
          about.columns[(int)Column.BestBidETHUSD] = (bestEthUsdBid.bidPrice * bestEthBid.bidPrice).ToString();
        }
      }
      TradingPair bestEthAsk = coin.Best(Coin.ethereum, false);
      if (bestEthAsk != null)
      {
        about.columns[(int)Column.BestAskETH] = bestEthAsk.askPrice.ToString();
        about.columns[(int)Column.BestAskETHExchange] = bestEthAsk.exchange.exchangeName.ToString();

        TradingPair bestEthUsdAsk = Coin.ethereum.Best(Coin.usd, false);
        if (bestEthUsdAsk != null)
        {
          about.columns[(int)Column.BestAskETHUSD] = (bestEthUsdAsk.askPrice * bestEthAsk.askPrice).ToString();
        }
      }

      TradingPair bestUsdBid = coin.Best(Coin.usd, true);
      if (bestUsdBid != null)
      {
        about.columns[(int)Column.BestBidUSD] = bestUsdBid.bidPrice.ToString();
        about.columns[(int)Column.BestBidUSDExchange] = bestUsdBid.exchange.exchangeName.ToString();
      }
      TradingPair bestUsdAsk = coin.Best(Coin.usd, false);
      if (bestUsdAsk != null)
      {
        about.columns[(int)Column.BestAskUSD] = bestUsdAsk.askPrice.ToString();
        about.columns[(int)Column.BestAskUSDExchange] = bestUsdAsk.exchange.exchangeName.ToString();
      }

      if (coin.coinMarketCapData != null)
      {
        about.columns[(int)Column.MarketCapUSD] = coin.coinMarketCapData.market_cap_usd ?? "?";
      }

      return about;
    }
  }
}
