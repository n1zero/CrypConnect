﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CrypConnect.CoinMarketCap;
using HD;

namespace CrypConnect
{
  public class Coin
  {
    #region Public Static 
    /// <summary>
    /// Reference to Bitcoin, for convenience.
    /// </summary>
    public static Coin bitcoin
    {
      get
      {
        return Coin.FromName("Bitcoin");
      }
    }

    /// <summary>
    /// Reference to Ethereum, for convenience.
    /// </summary>
    public static Coin ethereum
    {
      get
      {
        return Coin.FromName("Ethereum");
      }
    }

    /// <summary>
    /// Reference to United States Dollar, for convenience.
    /// </summary>
    public static Coin usd
    {
      get
      {
        return Coin.FromName("United States Dollar");
      }
    }

    /// <summary>
    /// Reference to Litecoin, for convenience.
    /// </summary>
    public static Coin litecoin
    {
      get
      {
        return Coin.FromName("Litecoin");
      }
    }
    #endregion

    #region Public Data
    public readonly string fullName;

    public delegate void OnUpdate(Coin coin, TradingPair tradingPair);

    /// <summary>
    /// Called when prices refresh on any TradingPair for this Coin.
    /// </summary>
    public event OnUpdate onPriceUpdate;

    /// <summary>
    /// Called when a TradingPair for this Coin has a status change.
    /// </summary>
    public event OnUpdate onStatusUpdate;

    /// <summary>
    /// Called anytime a new exchange pairing is listed.
    /// 
    /// onPriceUpdated is also called anytime this event occurs.
    /// </summary>
    public event OnUpdate onNewTradingPairListed;

    public bool hasValidTradingPairs
    {
      get
      {
        bool hasBid = false;
        bool hasAsk = false;
        foreach (var pair in exchangeInfo)
        {
          if (pair.Value.isInactive == false)
          {
            if (pair.Value.bidPrice > 0)
            {
              hasBid = true;
            }
            if (pair.Value.askPrice > 0)
            {
              hasAsk = true;
            }
            if (hasBid && hasAsk)
            {
              return true;
            }
          }
        }

        return false;
      }
    }

    public CoinMarketCapTickerJson coinMarketCapData;
    #endregion

    #region Private Data
    /// <summary>
    /// Cached in this form for performance.
    /// </summary>
    internal readonly string fullNameLower;

    readonly Dictionary<(ExchangeName, Coin baseCoin), TradingPair> exchangeInfo
      = new Dictionary<(ExchangeName, Coin baseCoin), TradingPair>();

    #endregion

    #region Public Properties
    public IEnumerable<TradingPair> allTradingPairs
    {
      get
      {
        return exchangeInfo.Values;
      }
    }
    #endregion

    #region Init
    Coin(
      string fullName)
    {
      Debug.Assert(ExchangeMonitor.instance.fullNameLowerToCoin.ContainsKey(fullName.ToLowerInvariant()) == false);
      Debug.Assert(string.IsNullOrWhiteSpace(fullName) == false);
      Debug.Assert(ExchangeMonitor.instance.aliasLowerToCoin.ContainsKey(fullName.ToLowerInvariant()) == false);
      Debug.Assert(ExchangeMonitor.instance.blacklistedFullNameLowerList.Contains(fullName.ToLowerInvariant()) == false);
      Debug.Assert(fullName.Equals("Ether", StringComparison.InvariantCultureIgnoreCase) == false);
      Debug.Assert(fullName.Equals("BTC", StringComparison.InvariantCultureIgnoreCase) == false);
      Debug.Assert(fullName.Equals("TetherUS", StringComparison.InvariantCultureIgnoreCase) == false);
      Debug.Assert(fullName.Equals("USDT", StringComparison.InvariantCultureIgnoreCase) == false);

      this.fullName = fullName;
      this.fullNameLower = fullName.ToLowerInvariant();

      ExchangeMonitor.instance.OnNewCoin(this);
    }

    public static Coin FromName(
      string fullName)
    {
      return CreateFromName2(fullName, true);
    }

    internal static Coin CreateFromName2(
      string fullName,
      bool skipCreationIfDoesNotExist = false)
    {
      // Blacklist
      if (ExchangeMonitor.instance.blacklistedFullNameLowerList.Contains(fullName.ToLowerInvariant()))
      {
        return null;
      }

      // Alias
      if (ExchangeMonitor.instance.aliasLowerToCoin.TryGetValue(fullName.ToLowerInvariant(), out Coin coin))
      {
        return coin;
      }

      // Existing Coin
      if (ExchangeMonitor.instance.fullNameLowerToCoin.TryGetValue(fullName.ToLowerInvariant(), out coin))
      {
        return coin;
      }

      if (skipCreationIfDoesNotExist)
      {
        return null;
      }

      // New Coin
      coin = new Coin(fullName);
      return coin;
    }

    public static Coin FromTicker(
      string ticker,
      ExchangeName onExchange)
    {
      ticker = ticker.ToLowerInvariant();

      Exchange exchange = ExchangeMonitor.instance.FindExchange(onExchange);
      Debug.Assert(exchange != null);

      if (exchange.tickerLowerToCoin.TryGetValue(ticker, out Coin coin))
      {
        return coin;
      }

      return null;
    }
    #endregion

    #region Public Read
    /// <summary>
    /// </summary>
    /// <param name="sellVsBuy">
    /// True: Sell this coin for baseCoin. False: Buy this coin with baseCoin.
    /// </param>
    /// <param name="baseCoinFullName"></param>
    /// <param name="exchangeName">
    /// If specified, only consider trades on this exchange
    /// </param>
    /// <returns></returns>
    public TradingPair Best(
      Coin baseCoin,
      bool sellVsBuy,
      ExchangeName? exchangeName = null)
    {
      TradingPair bestPair = null;
      decimal? bestValue = null;
      foreach (KeyValuePair<(ExchangeName, Coin baseCoin), TradingPair> pair
        in exchangeInfo)
      {
        if (exchangeName != null && pair.Key.Item1 != exchangeName.Value)
        { // Filter by exchange (optional)
          continue;
        }

        if (pair.Value.baseCoin != baseCoin)
        { // Filter by baseCoin (optional)
          continue;
        }

        if (pair.Value.isInactive)
        { // Ignore inactive pairs
          continue;
        }

        decimal value = sellVsBuy ? pair.Value.bidPrice : pair.Value.askPrice;
        if (value <= 0)
        { // No bid/ask to consider here
          continue;
        }

        if (bestValue == null
          || sellVsBuy && value > bestValue.Value
          || sellVsBuy == false && value < bestValue.Value)
        {
          bestValue = value;
          bestPair = pair.Value;
        }
      }

      return bestPair;
    }

    // TODO no tuple?
    public async Task<(decimal purchaseAmount, decimal quantity)> CalcPurchasePrice(
      ExchangeName askExchange,
      Coin askBaseCoin,
      decimal purchasePriceInBase)
    {
      decimal purchaseAmount = 0;
      decimal quantity = 0;

      Exchange exchange = ExchangeMonitor.instance.FindExchange(askExchange);
      OrderBook orderBook = await exchange.GetOrderBook(this, askBaseCoin);

      for (int i = 0; i < orderBook.asks.Length; i++)
      {
        Order order = orderBook.asks[i];
        decimal purchaseAmountFromOrder = Math.Min(purchasePriceInBase, 
          order.price * order.volume);

        purchaseAmount += purchaseAmountFromOrder;
        quantity += purchaseAmountFromOrder / order.price;
        purchasePriceInBase -= purchaseAmountFromOrder;

        if(purchasePriceInBase <= 0)
        {
          break;
        }
      }
     Console.WriteLine(purchaseAmount + " : " + quantity);
      return (purchaseAmount, quantity);
    }

    public async Task<decimal> CalcSellPrice(
      ExchangeName bidExchange, 
      Coin bidBaseCoin, 
      decimal quantityOfCoin)
    {
      decimal sellAmount = 0;

      Exchange exchange = ExchangeMonitor.instance.FindExchange(bidExchange);
      OrderBook orderBook = await exchange.GetOrderBook(this, bidBaseCoin);

      for (int i = 0; i < orderBook.bids.Length; i++)
      {
        Order order = orderBook.bids[i];
        decimal sellAmountFromOrder = Math.Min(quantityOfCoin, order.volume);

        sellAmount += sellAmountFromOrder * order.price;
        quantityOfCoin -= sellAmountFromOrder;

        if (quantityOfCoin <= 0)
        {
          break;
        }
      }

      return sellAmount;
    }

    public bool IsActiveOn(
      ExchangeName exchangeName)
    {
      return ExchangeMonitor.instance.FindExchange(exchangeName).IsCoinActive(this);
    }
    #endregion

    #region Internal Write
    internal void AddPair(
      TradingPair pair)
    {
      Debug.Assert(exchangeInfo.ContainsKey((pair.exchange.exchangeName, pair.baseCoin)) == false);

      exchangeInfo[(pair.exchange.exchangeName, pair.baseCoin)] = pair;
      onPriceUpdate?.Invoke(this, pair);
    }

    internal TradingPair AddPair(
      Exchange exchange,
      Coin baseCoin,
      decimal askPrice,
      decimal bidPrice)
    {
      (ExchangeName, Coin) key = (exchange.exchangeName, baseCoin);
      if (exchangeInfo.TryGetValue(key, out TradingPair pair))
      {
        pair.Update(askPrice, bidPrice);
      }
      else
      {
        pair = new TradingPair(exchange, baseCoin, this, askPrice, bidPrice);
        onNewTradingPairListed?.Invoke(this, pair);
      }

      onPriceUpdate?.Invoke(this, pair);
      return pair;
    }

    internal void UpdatePairStatus(
      Exchange exchange,
      Coin baseCoin,
      bool isInactive)
    {
      if (baseCoin == null)
      { // May be a blacklisted coin
        return;
      }

      (ExchangeName, Coin) key = (exchange.exchangeName, baseCoin);
      if (exchangeInfo.TryGetValue(key, out TradingPair pair) == false)
      {
        pair = new TradingPair(exchange, baseCoin, this, 0, 0, isInactive);
      }

      if (pair.isInactive != isInactive)
      {
        pair.isInactive = isInactive;
        onStatusUpdate?.Invoke(this, pair);
      }
    }
    #endregion

    #region Operators
    public static bool operator ==(
      Coin a,
      Coin b)
    {
      return a?.fullNameLower == b?.fullNameLower;
    }

    public static bool operator !=(
      Coin a,
      Coin b)
    {
      return a?.fullNameLower != b?.fullNameLower;
    }

    public override bool Equals(
      object obj)
    {
      if (obj is Coin otherCoin)
      {
        return fullNameLower == otherCoin.fullNameLower;
      }

      return false;
    }

    public override int GetHashCode()
    {
      return fullNameLower.GetHashCode();
    }

    public override string ToString()
    {
      return $"{fullName} {exchangeInfo.Count} pairs";
    }
    #endregion
  }
}
