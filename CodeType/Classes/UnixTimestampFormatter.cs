using System;
using System.Collections.Generic;
using System.Linq;
using GGNet.Formats;

namespace CodeType.Classes
{
    public class UnixTimestampFormatter : IFormatter<double>
    {
        private readonly bool _includeTimes;
        
        public UnixTimestampFormatter(Dictionary<DateTime, double> data)
        {
            _includeTimes = (data.Keys.Max() - data.Keys.Min()).TotalDays < 4;
        }

        public string Format(double value)
        {
            DateTime dateTimeValue = DateTimeOffset.FromUnixTimeSeconds((long) value).DateTime;
            string result = dateTimeValue.ToShortDateString();
            if (_includeTimes)
            {
                result += " " + dateTimeValue.ToShortTimeString();
            }

            return result;
        }
    }
}