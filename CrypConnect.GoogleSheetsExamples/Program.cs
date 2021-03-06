﻿using System;
using System.Collections.Generic;

namespace CrypConnect.GoogleSheetsExamples
{
  /// <summary>
  /// Getting Started:
  ///  - Follow Steps 1 here to create a client_id.json and place it
  ///  in the CrypConnect.GoogleSheetsExamples\bin directory 
  ///  https://developers.google.com/sheets/api/quickstart/dotnet
  ///  - Follow Steps 2 from the same guide to install the nuget package
  ///  - In GoogleSheetPriceMonitor change the sheet Id to one that you own
  ///  - Make changes to GoogleSheetPriceMonitor's read and write logic for your use case
  /// </summary>
  class Program
  {
    static void Main(
      string[] args)
    {
      GoogleSheetPriceMonitor priceMonitor = new GoogleSheetPriceMonitor();
      priceMonitor.Start();

      while(true)
      {
        if(Console.ReadLine().Equals("Quit", StringComparison.InvariantCultureIgnoreCase))
        {
          return;
        }
      }
    }
  }
}
