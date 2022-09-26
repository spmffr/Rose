using System;
using System.ComponentModel;
using ICG.NetCore.Utilities.Spreadsheet;

namespace FrondScope.Models
{
    public class SimpleExportData
    {
        //public string Title { get; set; }

        [DisplayName("Time(us)")]
        public uint Time { get; set; }

        [DisplayName("Chn0")]
        public UInt16 ch0 { get; set; }

        [DisplayName("Chn1")]
        public UInt16 ch1 { get; set; }

    }
}