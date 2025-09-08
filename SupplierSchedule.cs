using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LNAB
{
	internal class SupplierSchedule
	{
		public readonly TimeSpan WeekStart;

		public readonly TimeSpan WeekStop;

		public readonly TimeSpan SatStart;

		public readonly TimeSpan SatStop;

		public readonly TimeSpan SunStart;

		public readonly TimeSpan SunStop;

		public SupplierSchedule(string[] schArray)
		{
			DateTime dateTime;
			for (int i = 0; i < (int)schArray.Length; i++)
			{
				string[] strArrays = schArray[i].Split(new char[] { ';' });
				string str = strArrays[0];
				if (str == "0")
				{
					dateTime = DateTime.Parse(strArrays[1]);
					this.SunStart = dateTime.TimeOfDay;
					dateTime = DateTime.Parse(strArrays[2]);
					this.SunStop = dateTime.TimeOfDay;
				}
				else if (str == "6")
				{
					dateTime = DateTime.Parse(strArrays[1]);
					this.SatStart = dateTime.TimeOfDay;
					dateTime = DateTime.Parse(strArrays[2]);
					this.SatStop = dateTime.TimeOfDay;
				}
				else
				{
					dateTime = DateTime.Parse(strArrays[1]);
					this.WeekStart = dateTime.TimeOfDay;
					dateTime = DateTime.Parse(strArrays[2]);
					this.WeekStop = dateTime.TimeOfDay;
				}
			}
		}
	}
}
