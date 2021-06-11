using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;

namespace CadExSearch.Commons
{
#pragma warning disable IDE0011
    public static class BackgroundWorkerExtensions
    {
        public static async Task Transmiss<T1, T2>(this IEnumerable<T1> input, Func<T1, T2> func,
            IExtendedList<T2> output)
        {
            using var _ = new BackgroundWorker();
            _.Connect(input, func, output);
            _.RunWorkerAsync();
            await _.WaitForComplete();
        }

        public static async Task Transmiss<T1, T2>(this IEnumerable<T1> input, Func<T1, IEnumerable<T2>> func,
            IExtendedList<T2> output)
        {
            using var _ = new BackgroundWorker();
            _.Connect(input, o => func(o).ToArray(), output);
            _.RunWorkerAsync();
            await _.WaitForComplete();
        }

        // ReSharper disable once InconsistentNaming
        public static async Task CopyToUICollection<T>(this IEnumerable<T> input,
            IExtendedList<T> output)
        {
            await input.Transmiss(a => a, output);
        }

        public static void Connect<T1, T2>(this BackgroundWorker worker, IEnumerable<T1> input, Func<T1, T2> func,
            ICollection<T2> output, object @lock = null)
        {
            worker.WorkerReportsProgress = true;
            worker.DoWork += (_, _) =>
            {
                input.AsParallel().WithDegreeOfParallelism(8).ForAll(t =>
                {
                    var ret = func(t);
                    worker.ReportProgress(0, ret);
                });
            };
            worker.ProgressChanged += (_, args) =>
            {
                if (args?.UserState is not T2 ret) return;
                if (@lock is null)
                    output.Add(ret);
                else
                    lock (@lock)
                        output.Add(ret);
            };
        }

        public static void Connect<T1, T2>(this BackgroundWorker worker, IEnumerable<T1> input, Func<T1, T2[]> func,
            IExtendedList<T2> output, object @lock = null)
        {
            worker.WorkerReportsProgress = true;
            worker.DoWork += (_, _) =>
            {
                input.AsParallel().WithDegreeOfParallelism(8).ForAll(t =>
                {
                    var ret = func(t);
                    worker.ReportProgress(0, ret);
                });
            };
            worker.ProgressChanged += (_, args) =>
            {
                if (args?.UserState is not T2[] ret) return;
                if (@lock is null)
                    output.AddRange(ret);
                else
                    lock (@lock)
                        output.AddRange(ret);
            };
        }

        public static async Task WaitForComplete(this BackgroundWorker worker)
        {
            while (worker.IsBusy)
                await Task.Delay(10);
        }
    }
}
