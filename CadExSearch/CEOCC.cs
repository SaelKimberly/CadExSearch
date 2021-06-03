using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using CadExSearch.Commons;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using RestSharp;
using SaelSharp.Extensions;
using SaelSharp.Helpers;

namespace CadExSearch
{
    public delegate void OnProgressChanged(object sender, CEOCCResult result);

    public delegate CEOCCResult OnSingleResult(CEOCCResult cadn_desc_addr);

    public record CEOCCResult
    {
        public string Message { get; set; }

        public string CadNumber { get; set; }
        public string Address { get; set; }

        public string PortalAddress { get; set; }

        public Dictionary<string, string> Extended { get; set; } = default;
        public string PKK5Address { get; set; } = default;
        public string Status { get; set; } = default;

        public string DefaultView => $"{CadNumber}\t# {Status ?? "Неизвестный"}\t# {Address}";

        public bool IsFinal { get; set; }
        public bool IsFirst { get; set; }

        public int Count { get; set; }
        public int Total { get; set; }

        public TimeSpan Time { get; set; }
    }


    // ReSharper disable once InconsistentNaming
    public class CEOCC : ReactiveObject
    {
        public CEOCC()
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
                var r = s as CEOCCResult;
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
                .Throttle(TimeSpan.FromSeconds(2))
                .Subscribe(async s => await UpdateBySubject(s));

            this.WhenAnyValue(_ => _.SelectedRegion)
                .Throttle(TimeSpan.FromSeconds(2))
                .Subscribe(async s => await UpdateByRegion(s));

            this.WhenAnyValue(_ => _.SelectedSettlementType)
                .Throttle(TimeSpan.FromSeconds(2))
                .Subscribe(async s => await UpdateBySettlementType(s));

            this.WhenAnyValue(_ => _.SorterExpression, _ => _.SorterExpression)
                .Throttle(TimeSpan.FromSeconds(5))
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

            Parser = new HtmlParser();
            InitialConnect();
        }

        public static string UserAgent { get; } =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0";

        private RestClient Client { get; }
        public CookieContainer Cookie { get; set; } = new();
        private HtmlParser Parser { get; } = new();

        private Dictionary<string, string> URIs { get; } = new();

        [Reactive] private string InitialPage { get; set; }

        private string Referer { get; } = "https://rosreestr.gov.ru/wps/portal/p/cc_ib_portal_services/online_request";

        [Reactive] public (string, string) SelectedSubject { get; set; }
        [Reactive] public (string, string) SelectedRegion { get; set; }
        [Reactive] public (string, string) SelectedSettlementType { get; set; }
        [Reactive] public (string, string) SelectedSettlement { get; set; }
        [Reactive] public (string, string) SelectedStreetType { get; set; }

        [Reactive] public bool IsConnected { get; private set; }
        [Reactive] public string Message { get; private set; } = "";

        public ObservableCollectionExtended<(string, string)> Subjects { get; } = new();
        public ObservableCollectionExtended<(string, string)> Regions { get; } = new();
        public ObservableCollectionExtended<(string, string)> SettlementTypes { get; } = new();
        public ObservableCollectionExtended<(string, string)> Settlement { get; } = new();
        public ObservableCollectionExtended<(string, string)> StreetTypes { get; } = new();

        public ObservableCollectionExtended<CEOCCResult> RawFetchedResults { get; } = new();
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

                    if (!Regex.IsMatch(response.Content,
                        @"<input type=""radio"" name=""search_type"" value=""ADDRESS"".+>"))
                        throw new ApplicationException("Fetched page has invalid structure!");

                    InitialPage = response.Content;

                    var haddr1 = new Regex(@"<base href=""https://rosreestr.gov.ru/wps(?<haddr>[^""]+)");
                    URIs["session-addr"] = haddr1.Match(response.Content).Groups["haddr"].Value;

                    var haddr2 = new Regex(@"#captchaImage2'\)\.attr\('src',\s'(?<haddr>[^']+)");
                    URIs["captcha-addr"] = URIs["session-addr"] + haddr2.Match(response.Content).Groups["haddr"].Value;

                    var haddr3 = new Regex(@"<form action=""(?<haddr>[^""]+)");
                    URIs["form-addr"] = URIs["session-addr"] + haddr3.Match(response.Content).Groups["haddr"].Value;

                    //ReloadCaptcha();
                    //CaptchaTimer.Start();
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
                var subs = await CacheContext.CacheGetValues("subject");
                var strs = await CacheContext.CacheGetValues("STR");

                var subs_s = subs == null || !subs.Any();
                var strs_s = strs == null || !strs.Any();

                if (!string.IsNullOrEmpty(InitialPage) && (subs_s || strs_s))
                {
                    using var doc = await Parser.ParseDocumentAsync(InitialPage);
                    if (subs_s && !Subjects.Any())
                    {
                        var sub_s = (from s in doc.QuerySelectorAll("select#oSubjectId > option").Skip(1)
                            select (s.GetAttribute("value"), s.InnerHtml)).ToArray();
                        Subjects.AddRange(sub_s);
                        await CacheContext.CacheSetValues("subject",
                            sub_s.Select(s => $"{s.Item1}:{s.InnerHtml}").ToArray());
                    }

                    if (strs_s && !StreetTypes.Any())
                    {
                        await CacheContext.AddFork("subject", "STR");
                        var str_s = (from s in doc.QuerySelectorAll("select[name=street_type] > option").Skip(1)
                            select (s.GetAttribute("value"), s.InnerHtml)).ToArray();
                        StreetTypes.AddRange(str_s);
                        await CacheContext.CacheSetValues("STR",
                            str_s.Select(s => $"{s.Item1}:{s.InnerHtml}").ToArray());
                    }
                }
                else
                {
                    if (!Subjects.Any())
                        Subjects.AddRange(subs.Select(s => s.Split(':')).Select(s => (s[0], s[1])));

                    if (!StreetTypes.Any())
                        StreetTypes.AddRange(strs.Select(s => s.Split(':')).Select(s => (s[0], s[1])));
                }
            }

            IsBusy = false;
        }

        private async Task UpdateBySubject((string, string) subject)
        {
            IsBusy = true;
            if (subject == default) goto lbl;
            var (id, _) = subject;

            var regs = await CacheContext.CacheGetValues($"SUB:{id}");
            if (regs == null || !regs.Any())
            {
                await CacheContext.AddFork("subject", $"SUB:{id}");
                var response = await Client.ExecuteAsync(
                    new RestRequest($"/PA_RRORSrviceExtended/Servlet/ChildsRegionController?parentId={id}",
                            Method.GET)
                        .AddHeader("X-Requested-With", "XMLHttpRequest")
                        .AddHeader("X-Prototype-Version", "1.5.0_rc2")
                        .AddHeader("Connection", "keep-alive")
                        .AddHeader("Referer", Referer)
                        .AddHeader("Cache-Control", "max-age=0"));

                ReactiveCommand.Create(() =>
                {
                    Regions.Clear();
                    Regions.AddRange(response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                        m => (m.Groups["code"].Value, m.Groups["name"].Value)));
                }).ObserveOn(RxApp.MainThreadScheduler).Subscribe();
                
                await CacheContext.CacheSetValues($"SUB:{id}",
                    Regions.Select(s => $"{s.Item1}:{s.Item2}").ToArray());
            }
            else
            {
                Regions.Clear();
                Regions.AddRange(regs.Select(_ => _.Split(':')).Select(_ => (_[0], _[1])));
            }

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

            var setm = await CacheContext.CacheGetValues($"REG:S:{id}");
            var sett = await CacheContext.CacheGetValues($"REG:T:{id}");
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

                Settlement.Clear();
                Settlement.AddRange(response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)));

                await CacheContext.AddFork("subject", $"REG:S:{id}");
                await CacheContext.CacheSetValues($"REG:S:{id}",
                    Settlement.Select(s => $"{s.Item1}:{s.Item2}").ToArray());
            }
            else
            {
                Settlement.Clear();
                Settlement.AddRange(setm.Select(_ => _.Split(':')).Select(_ => (_[0], _[1])));
            }

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

                SettlementTypes.Clear();
                SettlementTypes.AddRange(response.Content.TryMatches(@"(?<code>\d+);(?<name>.+)",
                    m => (m.Groups["code"].Value, m.Groups["name"].Value)));

                await CacheContext.AddFork("subject", $"REG:T:{id}");
                await CacheContext.CacheSetValues($"REG:T:{id}",
                    SettlementTypes.Select(s => $"{s.Item1}:{s.Item2}").ToArray());
            }
            else
            {
                SettlementTypes.Clear();
                SettlementTypes.AddRange(sett.Select(_ => _.Split(':')).Select(_ => (_[0], _[1])));
            }

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
            var set = await CacheContext.CacheGetValues($"REG:{SelectedRegion}:S:{id}");
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

                var parser = new Regex(@"(?<code>\d+);(?<name>.+)");

                Settlement.Clear();
                await CacheContext.AddFork("subject", $"REG:{SelectedRegion}:S:{id}");
                foreach (Match m in parser.Matches(response.Content))
                    Settlement.Add((m.Groups["code"].Value, m.Groups["name"].Value));
                await CacheContext.CacheSetValues($"REG:{SelectedRegion}:S:{id}",
                    Settlement.Select(s => $"{s.Item1}:{s.Item2}").ToArray());
            }
            else
            {
                Settlement.Clear();
                SettlementTypes.AddRange(set.Select(_ => _.Split(':')).Select(_ => (_[0], _[1])));
            }

            lbl:
            SelectedSettlement = default;
            IsBusy = false;
        }

        private IComparer RefreshSorter(string sorter)
        {
            return SimpleComparer<CEOCCResult>.Of((r_s1, r_s2) =>
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
                return ms switch {<0 => -1, >0 => 1, _ => 0};
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

        private IEnumerable<CEOCCResult> ParsePage(IHtmlDocument page)
        {
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
                select new CEOCCResult
                {
                    CadNumber = Regex.Replace(cadn, @"(^\s*)|(\s*$)", ""),
                    Address = Regex.Replace(desc, @"(^\s*)|(\s*$)", ""),
                    PortalAddress = addr
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

            #endregion

            #region Current Page Parsing - LINQ Mode

            using (var cd = new CompositeDisposable())
            {
                ParsePage(page).AsParallel().WithDegreeOfParallelism(8)
                    .Select(r => erm(r))
                    .ToObservable()
                    .Subscribe(r => RawFetchedResults.Add(r with {Time = GetCurTime()}))
                    .DisposeWith(cd);
            }

            #endregion

            if (TotalFound <= 20) return true;

            var portlet_id = page.QuerySelector(".asa\\.portlet\\.id").InnerHtml;
            var page_links =
                from i in Enumerable.Range(0, TotalFound / 20 + (TotalFound % 20 > 0 ? 1 : 0) - 1)
                let uri = $"{URIs["form-addr"]}?online_request_search_page={i + 2}#{portlet_id}"
                select new RestRequest(
                        $"{URIs["form-addr"]}?online_request_search_page={i + 2}#{portlet_id}")
                    .AddHeader("Referer", Referer).AddHeader("Upgrade-Insecure-Requests", "1");
            Parallel.ForEach(page_links, () => new CompositeDisposable(), (l, pls, cd) =>
            {
                IRestResponse npRes = default;
                for (var i = 0; i < 10; i++)
                {
                    //lock (@lock)
                    npRes = new RestClient("https://rosreestr.gov.ru/wps")
                        {Timeout = -1, CookieContainer = Cookie, UserAgent = UserAgent}.Execute(l);
                    if (npRes.StatusCode == HttpStatusCode.OK)
                        break;
                    npRes = default;
                }

                if (npRes == default)
                {
                    Message = $"Не удалось получить страницу: {l.Resource}";
                    return cd;
                }

                var s_page = Parser.ParseDocument(npRes.Content);
                ParsePage(s_page).AsParallel().WithDegreeOfParallelism(8)
                    .Select(r => erm(r))
                    .ToObservable()
                    .Subscribe(r => RawFetchedResults.Add(r with {Time = GetCurTime()}))
                    .DisposeWith(cd);
                return cd;
            }, cd => cd.Dispose());
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
    }
}