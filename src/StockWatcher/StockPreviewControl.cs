using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StockWatcher
{
    public partial class StockPreviewControl : UserControl
    {
        private Timer timer = null;
        private static int currentIndex = -1;
        private static int maxIndex = -1;
        private int errorCount = 0;
        public StockPreviewControl(CSDeskBand.CSDeskBandWin w)
        {
            CheckForIllegalCrossThreadCalls = false;

            InitializeComponent();

            this.ContextMenu = new ContextMenu(new MenuItem[]
            {
//                new MenuItem("联系作者", new EventHandler((s, e) =>
//                {
//                    Util.Info($@"

// System.Windows.Forms.Application.ExecutablePath:{System.Windows.Forms.Application.ExecutablePath}
//System.Windows.Forms.Application.StartupPath:{System.Windows.Forms.Application.StartupPath}
//System.AppDomain.CurrentDomain.BaseDirectory:{System.AppDomain.CurrentDomain.BaseDirectory}");
//                })),
                new MenuItem("设置", new EventHandler((s, e) =>
                {
                    new frmSetting(this).ShowDialog();
                })),
            });
            ResetTimer();

            w.ShowDW(true);
        }

        public void ResetTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer.Dispose();
                currentIndex = maxIndex = -1;
                timer = null;
            }
            timer = new Timer()
            {
                Interval = StockConfig.RefreshInterval,
                Enabled = true
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            LoadSetting();
            if (currentIndex == -1)
            {
                UpdateStatus($"暂无监视对象");
            }
            else
            {
                Task.Run(async () =>
                {
                    var currentCode = StockConfig.StockList[currentIndex];
                    var stockModel = await GetStockInfo(currentCode);
                    if (stockModel == null)
                    {
                        UpdateStatus($"查询{currentCode}失败");
                    }
                    else
                    {
                        labelForStatus.Tag = stockModel;
                        UpdateStatus($"{stockModel.Name} {stockModel.CurrentPrice.ToString("F2")}\r\n{stockModel.LastClose.ToString("F2")} {(stockModel.IsUp ? "↑" : "↓")}" 
                         , stockModel.IsUp ? StockColor.Red : StockColor.Green);
                        //UpdateStatus($"{stockModel.Name} {stockModel.CurrentPrice.ToString("F2")}/{(stockModel.PricePercent*100).ToString("F1")}{(stockModel.IsUp ? "↑" : "↓")}" +
                        //    $"\r\n{stockModel.BuyPrice1}-{(stockModel.BuyAmount1/10000f).ToString("F2")} / {stockModel.SellPrice1}-{(stockModel.SellAmount1/10000f).ToString("F2")}" 
                        //    , stockModel.IsUp ? StockColor.Red : StockColor.Green);
                    }
                });
            }
        }

        private void UpdateStatus(string text, StockColor color = StockColor.Disabled)
        {
            switch (color)
            {
                case StockColor.Green:
                    labelForStatus.ForeColor = Color.FromArgb(0, 120, 0);
                    break;
                case StockColor.Red:
                    labelForStatus.ForeColor = Color.FromArgb(120, 0, 0);
                    break;
                case StockColor.Disabled:
                    labelForStatus.ForeColor = Color.White;
                    labelForStatus.Tag = null;
                    break;
            }
            labelForStatus.Text = text;
        }

        private void LoadSetting()
        {
            StockConfig.LoadSetting();
            maxIndex = StockConfig.StockList.Count-1;
            if (maxIndex == 0)
            {
                currentIndex = 0;
            }else if (maxIndex < 0)
            {
                currentIndex = -1;
            }
            else if (currentIndex > maxIndex)
            {
                currentIndex = 0;
            }
            else
            {
                ++currentIndex;
            }
        }

        private async Task<StockModel> GetStockInfo(string code)
        {
            try
            {
                var res = await InvokeSinaApi(code);
#if DEBUG
                //Util.Log($"服务器返回值【{res}】");
#endif
                var startAt = res.IndexOf("\"") + 1;
                if (startAt == -1)
                {
                    throw new Exception($"服务器返回数据异常：{res}");
                }
                var length = res.LastIndexOf("\"") - res.IndexOf("\"") - 1;
                var result = res.Substring(startAt, length);
                if (string.IsNullOrEmpty(result))
                {
                    throw new Exception($"服务器返回数据异常：{res}");
                }
                var arr = result.Split('~');
                if (arr.Length < 3)
                {
                    throw new Exception($"服务器返回数据异常：{res}");
                }
                return new StockModel()
                {
                    Code = code.Substring(code.Length - 6),
                    Name = arr[1],
					CurrentPrice = float.Parse(arr[3]),
					LastClose= float.Parse(arr[4]),
                    BuyPrice1= float.Parse(arr[6]),
                    BuyVolume1= 0,
                    SellPrice1= 0,
                    SellVolume1= 0
                };
            }
            catch (Exception ex)
            {
                errorCount++;
                //记录日志
                if (errorCount < 10)
                {
                    Util.Log($"查询股票【{code}】信息出错！", ex);
                }
                
                return null;
            }
        }

        private async Task<string> InvokeSinaApi(string code)
        {
			//var api = $"http://hq.sinajs.cn/list={code}";
			var api = $"http://qt.gtimg.cn/q=s_{code}";
			try
            {
                using (var client = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(5),
                })
                {
                    client.DefaultRequestHeaders.Add("Referer", "http://finance.sina.com.cn");
                    return await client.GetStringAsync(api);
                }
            }
            catch (Exception ex)
            {
                //记录日志
                Util.Log($"请求API【{api}】出错！", ex);
                return "";
            }
        }

        private void labelForStatus_DoubleClick(object sender, EventArgs e)
        {
            if (labelForStatus.Tag != null && labelForStatus.Tag is StockModel model)
            {
                Util.Info(
                    $@"代码：{model.Code} 
名称：{model.Name}
现价：{model.CurrentPrice}
买一：{model.BuyAmount1}
卖一：{model.SellVolume1}"
                , "查看详情");
            }
        }
    }

    class StockModel
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public float CurrentPrice { get; set; }
        public float LastClose { get; set; }
        public float PricePercent { get {
              return  (CurrentPrice - LastClose) / LastClose;
            } }
        public bool IsUp
        {
            get
            {
                return LastClose > 0;
            }
        }

        public float BuyPrice1 { get; set; }
        public int BuyAmount1 { get {
                return Convert.ToInt32( (this.BuyPrice1 * this.BuyVolume1) / 10000);
            }  }
        public float BuyVolume1 { get; set; }
        public float SellPrice1 { get; set; }
        public int SellAmount1 { get {
                return Convert.ToInt32 (SellPrice1 * SellVolume1 / 10000);
            } }
        public float SellVolume1 { get; set; }
    }

    enum StockColor
    {
        Green,
        Red,
        Disabled
    }
}
