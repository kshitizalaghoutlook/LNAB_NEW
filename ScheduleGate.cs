using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LNAB
{
    internal sealed class ScheduleGate : IDisposable
    {
        public bool Accepting { get; private set; }
        readonly SupplierSchedule s;
        readonly TimeSpan postStart = TimeSpan.FromMinutes(1);
        System.Threading.Timer t; DateTime lastRestartFor = DateTime.MinValue;

        public ScheduleGate(SupplierSchedule schedule) { s = schedule; }
        public void Start() => t = new System.Threading.Timer(_ => Tick(), null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        public void Dispose() => t?.Dispose();

        static (TimeSpan a, TimeSpan b) W(SupplierSchedule s, DayOfWeek d) =>
            d == DayOfWeek.Sunday ? (s.SunStart, s.SunStop) :
            d == DayOfWeek.Saturday ? (s.SatStart, s.SatStop) :
            (s.WeekStart, s.WeekStop);

        static bool InWin((TimeSpan a, TimeSpan b) w, TimeSpan now) =>
            (w.a == TimeSpan.Zero && w.b == TimeSpan.Zero) ? false :
            (w.a <= w.b ? (now >= w.a && now <= w.b) : (now >= w.a || now <= w.b)); // supports overnight

        static DateTime NextStart(SupplierSchedule s, DateTime now)
        {
            for (int i = 0; i < 8; i++)
            {
                var d = now.Date.AddDays(i); var w = W(s, d.DayOfWeek);
                if (w.a == TimeSpan.Zero && w.b == TimeSpan.Zero) continue;
                if (i > 0 || now.TimeOfDay <= w.a) return d + w.a;
            }
            return now.Date.AddDays(1);
        }

        void Tick()
        {
            var now = DateTime.Now;
            var w = W(s, now.DayOfWeek);

            // update accepting flag
            var on = InWin(w, now.TimeOfDay);
            if (on != Accepting) Accepting = on;

            // post-start restart once per window
            var nextStart = NextStart(s, now);
            var restartAt = nextStart + postStart;
            if (now >= restartAt && lastRestartFor.Date != nextStart.Date)
            {
                lastRestartFor = nextStart;
                TryRestart();
                return;
            }

            // next wake: end of current window, next start, or post-start time
            DateTime? end = (w.a == TimeSpan.Zero && w.b == TimeSpan.Zero) ? (DateTime?)null
                           : (w.a <= w.b ? now.Date + w.b : now.Date.AddDays(1) + w.b);

            DateTime next = DateTime.MaxValue;
            foreach (var c in new[] { restartAt, nextStart, end ?? DateTime.MaxValue })
                if (c > now && c < next) next = c;

            var due = next - now;
            if (due < TimeSpan.FromMilliseconds(250)) due = TimeSpan.FromMilliseconds(250);
            t.Change(due, Timeout.InfiniteTimeSpan);
        }

        static void TryRestart()
        {
            try { Application.Restart(); }
            catch { try { System.Diagnostics.Process.Start(Application.ExecutablePath); } catch { } Environment.Exit(0); }
        }
    }
}
