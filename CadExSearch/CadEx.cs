using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Data;
using Akavache;
using Akavache.Sqlite3;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using CadExSearch.Commons;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using RestSharp;
using TS.Utils.Pools;

#pragma warning disable IDE0011

namespace CadExSearch
{
    public delegate CadExResult OnSingleResult(CadExResult cadn_desc_addr);

    public record CadExResult
    {
        public string Message { get; set; }

        public string CadNumber { get; set; }
        public string Address { get; set; }

        public string PortalAddress { get; set; }

        public Dictionary<string, string> Extended { get; set; } = default;
        public string PKK5Address { get; set; } = default;
        public string Status { get; set; } = default;

        public string DefaultView => $"{CadNumber}\t# {Status ?? "Неизвестный"}\t# {Address}";

        public TimeSpan Time { get; set; }
    }


    // ReSharper disable InconsistentNaming
    // ReSharper disable IdentifierTypo
    public class CadEx : ReactiveObject
    {
        public CadEx()
        {
            Client = new RestClient("https://rosreestr.gov.ru/wps/")
            {
                CookieContainer = Cookie,
                Timeout = -1,
                UserAgent = UserAgent
            };
            Client.AddDefaultHeaders(new Dictionary<string, string>
            {
                {"Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8"},
                {"Accept-Language", "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3"},
                {"Connection", "keep-alive"}
            });

            FetchedResults = (ListCollectionView) CollectionViewSource.GetDefaultView(RawFetchedResults);
            FetchedResults.Filter = s =>
            {
                var r = s as CadExResult;
                if (string.IsNullOrWhiteSpace(FilterExpression) || FilterDirection == null) return true;
                return FilterDirection.SafeLet(_ => _ == Regex.IsMatch(r.DefaultView, FilterExpression));
            };

            this.WhenAnyValue(_ => _.Message)
                .Where(_ => _ != "")
                .Delay(TimeSpan.FromSeconds(5))
                .Subscribe(_ => Message = "");

            this.WhenAnyValue(_ => _.InitialPage)
                .Subscribe(async _ =>
                    await PreloadCommonAssets());

            this.WhenAnyValue(_ => _.SelectedSubject)
                .Subscribe(async s =>
                {
                    await UpdateBySubject(s);
                    RegionAvailable = s != default;
                });

            this.WhenAnyValue(_ => _.SelectedRegion)
                .Subscribe(async s =>
                {
                    await UpdateByRegion(s);
                    SettlementAvailable = s != default;
                    SettlementTypeAvailable = s != default;
                });

            this.WhenAnyValue(_ => _.SelectedSettlementType)
                .Subscribe(async s =>
                {
                    await UpdateBySettlementType(s);
                    SettlementAvailable = s != default;
                });

            this.WhenAnyValue(_ => _.SorterExpression, _ => _.SorterExpression)
                .Throttle(TimeSpan.FromSeconds(3))
                .Select(x => RefreshSorter(x.Item1))
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    FetchedResults.CustomSort = _;
                    FetchedResults.Refresh();
                });

            RawFetchedResults.CollectionChanged += (s, e) =>
            {
                TotalFetch = RawFetchedResults.Count;
                TotalShown = FetchedResults.Count;
            };

            this.WhenAnyValue(_ => _.FilterExpression, _ => _.FilterDirection)
                .Subscribe(_ => FetchedResults.Refresh());

            InitialConnect();
        }

        [Reactive] public (string, string) SelectedSubject { get; set; }
        [Reactive] public (string, string) SelectedRegion { get; set; }
        [Reactive] public (string, string) SelectedSettlementType { get; set; }
        [Reactive] public (string, string) SelectedSettlement { get; set; }
        [Reactive] public (string, string) SelectedStreetType { get; set; }

        [Reactive] public bool RegionAvailable { get; private set; }
        [Reactive] public bool SettlementTypeAvailable { get; private set; }
        [Reactive] public bool SettlementAvailable { get; private set; }

        [Reactive] public bool IsConnected { get; private set; }
        [Reactive] public string Message { get; private set; } = "";

        public ObservableCollectionExtended<(string, string)> Subjects { get; } = new();
        public ObservableCollectionExtended<(string, string)> Regions { get; } = new();
        public ObservableCollectionExtended<(string, string)> SettlementTypes { get; } = new();
        public ObservableCollectionExtended<(string, string)> Settlement { get; } = new();
        public ObservableCollectionExtended<(string, string)> StreetTypes { get; } = new();

        public ObservableCollectionExtended<CadExResult> RawFetchedResults { get; } = new();
        public ListCollectionView FetchedResults { get; }

        [Reactive] public string FilterExpression { get; set; }
        [Reactive] public bool? FilterDirection { get; set; }

        [Reactive] public string SorterExpression { get; set; }
        [Reactive] public bool? SortDirection { get; set; }

        [Reactive] public int TotalFound { get; private set; }
        [Reactive] public int TotalFetch { get; private set; }
        [Reactive] public int TotalShown { get; private set; }

        [Reactive] public bool IsBusy { get; private set; }
        [Reactive] public bool? UseResultModifyerIfExists { get; set; } = true;
        public OnSingleResult EachResultModifier { get; set; } = default;

        private async void InitialConnect()
        {
            if (IsConnected) return;
            while (!IsConnected)
                try
                {
                    var response = await Client.ExecuteAsync(
                        new RestRequest("/portal/p/cc_ib_portal_services/online_request", Method.GET)
                            .AddHeader("Upgrade-Insecure-Requests", "1")
                            .AddHeader("Cache-Control", "max-age=0"));

                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new ApplicationException("Cannot fetch page from RR!");

                    if (!response.Content.TryMatch(
                        @"<input type=""radio"" name=""search_type"" value=""ADDRESS"".+>", out _))
                        throw new ApplicationException("Fetched page has invalid structure!");

                    InitialPage = response.Content;

                    URIs["session-addr"] = response.Content.TryMatch(
                        @"<base href=""https://rosreestr.gov.ru/wps(?<haddr>[^""]+)", _ => _.Groups["haddr"].Value);

                    URIs["captcha-addr"] = URIs["session-addr"] + response.Content.TryMatch(
                        @"#captchaImage2'\)\.attr\('src',\s'(?<haddr>[^']+)", _ => _.Groups["haddr"].Value);

                    URIs["form-addr"] = URIs["session-addr"] + response.Content.TryMatch(
                        @"<form action=""(?<haddr>[^""]+)", _ => _.Groups["haddr"].Value);

                    IsConnected = true;
                }
                catch (Exception e)
                {
                    Message = e.Message + "\nНе удалось подключиться. Повторная попытка через 5 секунд...";
                    await Task.Delay(new TimeSpan(0, 0, 5));
                }
        }

        private async Task PreloadCommonAssets()
        {
            IsBusy = true;
            if (!Subjects.Any() || !StreetTypes.Any())
            {
                var subs = CacheGetValues("subject");
                var strs = CacheGetValues("STR");

                if (!string.IsNullOrEmpty(InitialPage) && !(subs?.Any() ?? strs?.Any() ?? false))
                {
                    using var doc = await Parser.ParseDocumentAsync(InitialPage);
                    if (!(subs?.Any() ?? false) && !Subjects.Any())
                    {
                        subs = (from s in doc.QuerySelectorAll("select#oSubjectId > option").Skip(1)
                            select (s.GetAttribute("value"), s.InnerHtml)).ToArray();
                        CacheSetValues("subject", subs);
                    }

                    if (!(strs?.Any() ?? false) && !StreetTypes.Any())
                    {
                        strs = (from s in doc.QuerySelectorAll("select[name=street_type] > option").Skip(1)
                            select (s.GetAttribute("value"), s.InnerHtml)).ToArray();
                        StreetTypes.AddRange(strs);
                        CacheSetValues("STR", strs);
                    }
                }
                else
                {
                    if (!Subjects.Any())
                        Subjects.AddRange(subs ?? Array.Empty<(string, string)>());

                    if (!StreetTypes.Any())
                        StreetTypes.AddRange(strs ?? Array.Empty<(string, string)>());
                }
            }

            IsBusy = false;
        }

        private async Task UpdateBySubject((string, string) subject)
        {
            IsBusy = true;
            if (subject == default) goto lbl;
            var (id, _) = subject;
            var regs = CacheGetValues($"SUB:{id}");
            if (regs == null || !regs.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest($"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={id}",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                regs = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                CacheSetValues($"SUB:{id}", regs);
            }

            await ReactiveCommand.Create(() =>
                {
                    Regions.Clear();
                    Regions.AddRange(regs);
                }, null, RxApp.MainThreadScheduler)
                .Execute();

            lbl:
            SelectedRegion = default;
            SelectedSettlement = default;
            SelectedSettlementType = default;
            IsBusy = false;
        }

        private async Task UpdateByRegion((string, string) region)
        {
            IsBusy = true;
            if (region == default) goto lbl;
            var (id, _) = region;

            var setm = CacheGetValues($"REG:S:{id}");
            var sett = CacheGetValues($"REG:T:{id}");
            if (setm == null || !setm.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest(
                            $"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={id}&settlement_type=-1&add_settlement_type=true",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                setm = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                CacheSetValues($"REG:S:{id}", setm);
            }

            await ReactiveCommand.Create(() =>
                {
                    Settlement.Clear();
                    Settlement.AddRange(setm);
                }, null, RxApp.MainThreadScheduler)
                .Execute();

            if (sett == null || !sett.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest(
                            $"/PA_RRORSrviceExtended/Servlet/ChildsRegionTypesController?parentId={id}",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                sett = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                CacheSetValues($"REG:T:{id}", sett);
            }

            await ReactiveCommand.Create(() =>
            {
                SettlementTypes.Clear();
                SettlementTypes.AddRange(sett);
            }, null, RxApp.MainThreadScheduler).Execute();

            lbl:
            SelectedSettlement = default;
            SelectedSettlementType = default;
            IsBusy = false;
        }

        private async Task UpdateBySettlementType((string, string) type)
        {
            IsBusy = true;
            if (SelectedRegion == default || type == default) goto lbl;

            var (id, desc) = type;
            var set = CacheGetValues($"REG:{SelectedRegion}:S:{id}");
            if (set == null || !set.Any())
            {
                var response = await Client.ExecuteAsync(
                    new RestRequest(
                            $"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={SelectedRegion}&settlement_type={id}&add_settlement_type=true",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                set = response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)).ToArray();

                CacheSetValues($"REG:{SelectedRegion}:S:{id}", Settlement);
            }

            await ReactiveCommand.Create(() =>
                {
                    Settlement.Clear();
                    Settlement.AddRange(set);
                }, null, RxApp.MainThreadScheduler)
                .Execute();

            lbl:
            SelectedSettlement = default;
            IsBusy = false;
        }

        private IComparer RefreshSorter(string sorter)
        {
            return SimpleComparer<CadExResult>.Of((r_s1, r_s2) =>
            {
                if (SorterExpression.IsNullOrWhitespace()) return 0;

                var s1 = r_s1.DefaultView;
                var s2 = r_s2.DefaultView;

                if (!SorterExpression.TryToRegex(out var reg)) return 0;
                var sorts = from m in Regex.Matches(SorterExpression, @"\(\?\<(?<sort>s\d+)\>")
                    let ret = m.Groups["sort"].Value
                    let wgt = int.Parse(Regex.Match(ret, @"(?<num>\d+)").Groups["num"].Value)
                    orderby wgt
                    select (ret, wgt);
                var im1 = s1.SafeLet(reg.IsMatch);
                var im2 = s2.SafeLet(reg.IsMatch);

                if (!im1 || !im2)
                    return (im1, im2) switch {(true, false) => 1, (false, true) => -1, _ => 0};

                var cmp = StringComparer.FromComparison(StringComparison.OrdinalIgnoreCase);
                var ms = 0;
                foreach (var (ret, wgt) in sorts)
                    ms += (reg.Match(s1).Groups[ret].Value, reg.Match(s2).Groups[ret].Value) switch
                    {
                        var (m1, m2) when int.TryParse(m1, out var m1d) && int.TryParse(m2, out var m2d) =>
                            (m1d > m2d ? 1 : m1d == m2d ? 0 : -1) * wgt,
                        var (m1, m2) when m1.Length != m2.Length =>
                            (m1.Length > m2.Length ? 1 : -1) * wgt,
                        var (m1, m2) =>
                            cmp.Compare(m1, m2) * wgt
                    };
                return ms switch {< 0 => -1, > 0 => 1, _ => 0};
            }, SortDirection);
        }

        private async Task<(bool, IHtmlDocument)> PrepareByPage(string content)
        {
            #region Preparing For Parsing. Getting Messages & Errors

            var page = await Parser.ParseDocumentAsync(content);
            //Find information and error messages on the first results page (or main page, if failed).
            var messages = page.QuerySelectorAll("td.infomsg1 span.t12");
            if (messages.Any())
            {
                var msg = Regex.Replace(messages.First().InnerHtml,
                    @"(^\s*)|(\s*$)|(&nbsp;)|(\n)|(\t)|(<[^>]+>)", "");
                var failed = !msg.StartsWith("Найдено");
                Message = msg;
                if (failed) return (false, default);
            }

            // Results count parsing from first results page.
            var page_stats = page.QuerySelectorAll("div#pg_stats");
            if (!page_stats.Any())
            {
                Message = "Не удалось обработать страницу!";
                return (false, default);
            }

            TotalFound = page_stats.First().InnerHtml.TryMatch(@"\D*(?<all>\d+)", out var m)
                ? m.Select("all", int.Parse)
                : 0;

            return (true, page);

            #endregion
        }

        private IEnumerable<CadExResult> ParsePage(IHtmlDocument page, Func<TimeSpan> timeFunc)
        {
            var erm = UseResultModifyerIfExists != null ? EachResultModifier ?? (s => s) : s => s;

            return from node in page.QuerySelectorAll("tr[id^=js_oTr]")
                let cn = node.QuerySelector("td:nth-child(2)").InnerHtml
                let cadn = string.IsNullOrWhiteSpace(cn)
                    ? ""
                    : Regex.Match(cn, ">(?<cn>[^<]+)<").Groups["cn"].Value
                let addr =
                    $"{URIs["session-addr"]}{node.QuerySelector("td:nth-child(1) > a").GetAttribute("href")}"
                let desc = Regex.Replace(
                    Regex.Replace(node.QuerySelector("td:nth-child(1) > a").InnerHtml, @"(\n)|(\s{2,})", " "),
                    @"&[^;]+;", "")
                where cadn != ""
                select erm(new CadExResult
                    {
                        CadNumber = Regex.Replace(cadn, @"(^\s*)|(\s*$)", ""),
                        Address = Regex.Replace(desc, @"(^\s*)|(\s*$)", ""),
                        PortalAddress = addr
                    }) with
                    {
                        Time = timeFunc?.Invoke() ?? default
                    };
        }

        private IRestRequest AddHeaders(IRestRequest request, string street, string house = "", string building = "",
            string structure = "", string apartment = "")
        {
            return request.AddHeader("Content-Type", "application/x-www-form-urlencoded")
                .AddHeader("Origin", "https://rosreestr.gov.ru")
                .AddHeader("Referer", "https://rosreestr.gov.ru/wps/portal/online_request")
                .AddHeader("Upgrade-Insecure-Requests", "1")
                .AddParameter("search_action", "true")
                //SCRAM
                .AddParameter("subject", "")
                .AddParameter("region", "")
                .AddParameter("settlement", SelectedSettlement.Item1)
                .AddParameter("cad_num", "")
                .AddParameter("start_position", "59")
                .AddParameter("obj_num", "")
                .AddParameter("old_number", "")

                //FILLED FIELDS
                .AddParameter("search_type", "ADDRESS")
                .AddParameter("subject_id", SelectedSubject.Item1)
                .AddParameter("region_id", SelectedRegion.Item1)
                .AddParameter("settlement_id", SelectedSettlement.Item1)
                .AddParameter("settlement_type",
                    SelectedSettlementType == default ? "-1" : SelectedSettlementType.Item1)
                .AddParameter("street_type",
                    SelectedStreetType == default ? "str0" : SelectedStreetType.Item1)
                .AddParameter("street", Regex.Replace(street, @"(^\s*)|(\s*$)", ""))
                .AddParameter("house", Regex.Replace(house, @"(^\s*)|(\s*$)", ""))
                .AddParameter("building", Regex.Replace(building, @"(^\s*)|(\s*$)", ""))
                .AddParameter("structure", Regex.Replace(structure, @"(^\s*)|(\s*$)", ""))
                .AddParameter("apartment", Regex.Replace(apartment, @"(^\s*)|(\s*$)", ""))
                //SCRAM
                .AddParameter("right_reg", "")
                .AddParameter("encumbrance_reg", "");
        }

        private IRestRequest AddHeaders(IRestRequest request, string cad_number)
        {
            return request.AddHeader("Content-Type", "application/x-www-form-urlencoded")
                .AddHeader("Origin", "https://rosreestr.gov.ru")
                .AddHeader("Referer", "https://rosreestr.gov.ru/wps/portal/online_request")
                .AddHeader("Upgrade-Insecure-Requests", "1")
                .AddParameter("search_action", "true")
                //SCRAM
                .AddParameter("subject", "")
                .AddParameter("region", "")
                .AddParameter("settlement", SelectedSettlement.Item1 ?? "")
                .AddParameter("start_position", "59")
                .AddParameter("obj_num", "")
                .AddParameter("old_number", "")
                //.AddParameter("o_subject_id", "110000000000")
                .AddParameter("subject_id", "")
                .AddParameter("region_id", "")
                .AddParameter("settlement_id", "")
                .AddParameter("settlement_type", "-1")
                .AddParameter("street_type", "str0")
                .AddParameter("street", "")
                .AddParameter("house", "")
                .AddParameter("building", "")
                .AddParameter("structure", "")
                .AddParameter("apartment", "")
                //FILLED FIELDS
                .AddParameter("search_type", "CAD_NUMBER")
                .AddParameter("cad_num", cad_number)

                //SCRAM
                .AddParameter("right_reg", "")
                .AddParameter("encumbrance_reg", "");

            //.AddParameter("captchaText", EnteredCaptcha);
        }

        private async Task<bool> StartFetching(IRestRequest initialRequest)
        {
            #region Stage1 : Prepare

            var sw = new Stopwatch();
            sw.Start();

            TimeSpan GetCurTime()
            {
                return sw.Elapsed;
            }

            var response = await Client.ExecuteAsync(initialRequest);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Message = "Не удалось получить данные по вашему запросу!";
                return false;
            }

            var (success, page) = await PrepareByPage(response.Content);
            if (!success) return false;

            var erm = UseResultModifyerIfExists != null ? EachResultModifier ?? (s => s) : s => s;

            var cmd = ReactiveCommand.Create<IEnumerable<CadExResult>>(r =>
            {
                RawFetchedResults.Clear();
                RawFetchedResults.AddRange(r);
            }, null, RxApp.MainThreadScheduler);

            #endregion

            #region Current Page Parsing - LINQ Mode

            ParsePage(page, GetCurTime)
                .Select(r => erm(r))
                .ToObservable()//.ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(r => cmd.Execute(new[] {r}).Wait());

            #endregion

            if (TotalFound <= 20) return true;

            var portlet_id = page.QuerySelector(".asa\\.portlet\\.id").InnerHtml;

            var t1 = new TransformBlock<int, IRestResponse>(async i =>
                    await new RestClient("https://rosreestr.gov.ru/wps")
                        .ExecuteAsync(
                            new RestRequest($"{URIs["form-addr"]}?online_request_search_page={i + 2}#{portlet_id}")
                                .AddHeader("Referer", Referer).AddHeader("Upgrade-Insecure-Requests", "1")),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 8
                });

            var t2 = new TransformBlock<IRestResponse, CadExResult[]>(async rr =>
            {
                var ret = rr is {StatusCode: HttpStatusCode.OK} ? await Parser.ParseDocumentAsync(rr.Content) : null;
                return ret is null
                    ? Array.Empty<CadExResult>()
                    : ParsePage(ret, GetCurTime).Select(r => erm(r) with {Time = GetCurTime()}).ToArray();
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 8
            });

            
            t2.AsObservable().ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(r => ReactiveCommand.Create(() =>
                {
                    RawFetchedResults.Clear();
                    RawFetchedResults.AddRange(r);
                }).Execute().Wait());

            t1.LinkTo(t2);
            
            for (var i = 0; i < TotalFound / 20 + (TotalFound % 20 > 0 ? 1 : 0) - 1; i++) t1.Post(i);
            t1.Complete();

            //a1.Completion.Wait();
            return true;
        }

        public async Task<bool> DownloadResults(string street, string house = "", string building = "",
            string structure = "",
            string apartment = "")
        {
            if (IsBusy) return false;
            IsBusy = true;
            var initialRequest = AddHeaders(new RestRequest(URIs["form-addr"], Method.POST), street, house,
                building,
                structure, apartment);
            return await StartFetching(initialRequest).ContinueWith(t => IsBusy = false);
        }

        public async Task<bool> DownloadResults(string cad_number)
        {
            if (IsBusy) return false;
            IsBusy = true;
            var initialRequest = AddHeaders(new RestRequest(URIs["form-addr"], Method.POST), cad_number);
            return await StartFetching(initialRequest).ContinueWith(t => IsBusy = false);
        }

        public void NewSession(bool clear)
        {
            if (clear)
            {
                RawFetchedResults.Clear();
                TotalFound = 0;
            }

            SelectedSubject = default;
        }

        #region Cache

        private record CacheRecord(string Id, (string, string) Value, string Type, string BaseId);

        public static SHA512 SHA { get; } = SHA512.Create();

        public static string GetId(string data)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(data));
            var hash = Convert.ToBase64String(SHA.ComputeHash(ms));
            return Regex.Replace(hash, @"(\W|\d)", "").ToUpper(CultureInfo.InvariantCulture)[..20];
        }

        private static (string, string)[] CacheGetValues(string root)
        {
            using (GlobalLock.LockSync())
            {
                using var cache = new SqlRawPersistentBlobCache(".\\CadEx.Cache.db3", RxApp.TaskpoolScheduler);
                var recs = cache.GetAllObjects<CacheRecord>().Wait();
                return recs.Where(c => c.BaseId == root && c.Type == "value").Select(v => v.Value).ToArray();
            }
        }

        private static void CacheSetValues(string root, IEnumerable<(string, string)> values)
        {
            var val = values as (string, string)[] ?? values.ToArray();
            if (!val.Any()) return;
            var recs = val.ToDictionary(s => GetId($"{s.Item1}:{s.Item2}"),
                s => new CacheRecord(GetId($"{s.Item1}:{s.Item2}"), s, "value", root));
            using (GlobalLock.LockSync())
            {
                using var cache = new SqlRawPersistentBlobCache(".\\CadEx.Cache.db3", RxApp.TaskpoolScheduler);
                using (cache.InsertAllObjects(recs).Subscribe())
                {
                }

                using (cache.Flush().Subscribe())
                {
                }
            }
        }

        #endregion

        #region Internal Fields

        public static CookieContainer Cookie { get; } = new();

        public static string UserAgent { get; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0";

        private RestClient Client { get; }
        private HtmlParser Parser { get; } = new();

        private Dictionary<string, string> URIs { get; } = new();
        [Reactive] private string InitialPage { get; set; }

        private string Referer { get; } = "https://rosreestr.gov.ru/wps/portal/p/cc_ib_portal_services/online_request";

        #endregion
    }
}