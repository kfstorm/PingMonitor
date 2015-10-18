using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.Common.Auxiliary;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;

namespace Kfstorm.PingMonitor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private PingClient _ping;
        private ObservableDataSource<PingResult> _ds;
        private IPlotterElement[] _plotterChildren;

        private void BtnStart_OnClick(object sender, RoutedEventArgs e)
        {
            if (_ping == null)
            {
                Debug.Assert(_plotterChildren == null);
                Debug.Assert(_ds == null);
                try
                {
                    var host = (tbHost.Text ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(host))
                    {
                        throw new ArgumentException("Host can not be empty.");
                    }
                    var interval = TimeSpan.Parse((tbInterval.Text ?? string.Empty));
                    if (interval <= TimeSpan.Zero)
                    {
                        throw new ArgumentException("Interval must be greater than zero.");
                    }
                    var timeout = TimeSpan.Parse((tbTimeout.Text ?? string.Empty));
                    if (timeout <= TimeSpan.Zero)
                    {
                        throw new ArgumentException("Timeout must be greater than zero.");
                    }
                    _ping = new PingClient(host, interval, timeout);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
                _ds = new ObservableDataSource<PingResult>();
                _ping.PingTick += PingOnPingTick;
                //_ping.PingException += PingOnPingException;
                _ds.SetXMapping(item => dateAxis.ConvertToDouble(item.DateTime));
                _ds.SetYMapping(item => item.TimeCost.TotalMilliseconds);
                _plotterChildren = new IPlotterElement[]
                {
                    new LineGraph(_ds)
                    {
                        LinePen = new Pen(new SolidColorBrush(Colors.Green), 2),
                        Description = new PenDescription(_ping.Host)
                    },
                    new MarkerPointsGraph(_ds) {Marker = new CirclePointMarker {Size = 5}}
                };
                foreach (var child in _plotterChildren)
                {
                    plotter.AddChild(child);
                }

                Header.Content = string.Format("Ping {0}", _ping.Host);
            }
            _ping.Start();

            btnStart.IsEnabled = false;
            btnPause.IsEnabled = true;
            pnSettings.IsEnabled = false;
        }

        //private void PingOnPingException(object sender, PingExceptionEventArgs pingExceptionEventArgs)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        var sb = new StringBuilder();
        //        var ex = pingExceptionEventArgs.Exception;
        //        while (ex != null)
        //        {
        //            sb.AppendLine(ex.Message);
        //            ex = ex.InnerException;
        //        }
        //        if (sb.Length == 0)
        //        {
        //            sb.AppendLine("Unknown error occurred.");
        //        }
        //        sb.AppendLine("Would you like to retry ?");

        //        if (MessageBox.Show(sb.ToString(), "Ping failed", MessageBoxButton.YesNo, MessageBoxImage.Warning) ==
        //            MessageBoxResult.No)
        //        {
        //            pingExceptionEventArgs.Cancel = true;
        //        }
        //    });

        //    if (pingExceptionEventArgs.Cancel)
        //    {
        //        Dispatcher.BeginInvoke(new Action(() =>
        //        {
        //            if (_ds.Collection.Count > 0)
        //            {
        //                BtnPause_OnClick(null, null);
        //            }
        //            else
        //            {
        //                BtnStop_OnClick(null,null);
        //            }
        //        }));
        //    }
        //}

        private void PingOnPingTick(object sender, PingTickEventArgs pingTickEventArgs)
        {
            Debug.Assert(_ds != null);
            _ds.AppendAsync(Dispatcher, pingTickEventArgs.Result);
        }

        private void BtnPause_OnClick(object sender, RoutedEventArgs e)
        {
            Debug.Assert(_ping != null);
            _ping.Stop();
            btnStart.IsEnabled = true;
            btnPause.IsEnabled = false;
        }

        private void BtnStop_OnClick(object sender, RoutedEventArgs e)
        {
            if (_ping != null)
            {
                _ping.PingTick -= PingOnPingTick;
                _ping.Stop();
                _ping = null;
                Debug.Assert(_plotterChildren != null);
                Debug.Assert(_ds != null);
                foreach (var child in _plotterChildren)
                {
                    plotter.Children.Remove(child);
                }
                _plotterChildren = null;
                _ds = null;
            }

            btnStart.IsEnabled = true;
            btnPause.IsEnabled = false;
            pnSettings.IsEnabled = true;
        }
    }
}
