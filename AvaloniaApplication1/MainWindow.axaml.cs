using System;
using System.Collections.Generic;
using System.Xml;
using System.Device.Location;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RangeSlider.Avalonia.Controls.Primitives;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using IFeature = Mapsui.IFeature;
using Point = Avalonia.Point;
using Mapsui.Extensions;
using Mapsui.Nts.Extensions;
using Mapsui.Projections;
using Mapsui.Styles;

namespace AvaloniaApplication1;

public partial class MainWindow : Window
{
    //zoznamy dat co sa na zaciatku nacitaju a neskor sa s nimi pracuje
    List<Zaznam> zaznamy;
    private int lowerBound = 0;
    private int upperBound = 0;
    private List<double> elevations;
    private List<double> speeds;
    private List<double> temperatures;
    private List<double> heartrates;
    private List<double> climbs;
    private List<double> descends;
    private List<string> times;
    private MemoryLayer lineLayer;
    //private Action<AvaloniaPropertyChangedEventArgs> debounceReload; //nepouzity kod
    public MainWindow()
    {
        //debounceReload  = Debounce((AvaloniaPropertyChangedEventArgs e) => reload(e), 50);   //nepouzity kod
        InitializeComponent();
        zaznamy = new List<Zaznam>();
        
        //kontrola, ci sa nasiel subor
        XmlDocument doc = new XmlDocument();
        if (WelcomeWindow.fileName != null)
        {
            doc.Load(WelcomeWindow.fileName);
        }
        else
        {
            Close();
        }
        
        //dekodovanie a nacitanie suboru
        XmlNodeList coordinates = doc.GetElementsByTagName("trkpt");
        foreach (XmlNode node in coordinates)
        {
            Zaznam zaznam = new Zaznam();
            zaznam.Latitude = double.Parse(node.Attributes["lat"].Value, System.Globalization.CultureInfo.InvariantCulture);
            zaznam.Longitude = double.Parse(node.Attributes["lon"].Value, System.Globalization.CultureInfo.InvariantCulture);
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Name == "time")
                {
                    zaznam.Time = DateTime.Parse(childNode.InnerText);
                }

                if (childNode.Name == "ele")
                {
                    zaznam.Elevation = double.Parse(childNode.InnerText, System.Globalization.CultureInfo.InvariantCulture);
                }

                if (childNode.Name == "extensions")
                {
                    foreach (XmlNode childNodeChildNode in childNode.ChildNodes)
                    {
                        if (childNodeChildNode.Name == "ns3:TrackPointExtension")
                        {
                            foreach (XmlNode childNodeChildNodeChildNode in childNodeChildNode.ChildNodes)
                            {
                                if (childNodeChildNodeChildNode.Name == "ns3:atemp")
                                {
                                    zaznam.Temperature = double.Parse(childNodeChildNodeChildNode.InnerText, System.Globalization.CultureInfo.InvariantCulture);
                                }
                                if (childNodeChildNodeChildNode.Name == "ns3:hr")
                                {
                                    zaznam.HeartRate = double.Parse(childNodeChildNodeChildNode.InnerText, System.Globalization.CultureInfo.InvariantCulture);
                                }
                            }
                        }
                    }
                }
            }
            zaznamy.Add(zaznam);
        }

        XmlNode name = doc.GetElementsByTagName("name")[0];
        int duration2 = 0;
        double distance = 0; 
        
        //ziskanie dat z objektu do zoznamov
        List<double> speeds2 = new List<double>();
        List<Coordinate> coordinates2 = new List<Coordinate>();
        for (int i = 0; i < zaznamy.Count - 1; i++)
        {
            if (zaznamy[i + 1].Time.Value.Subtract(zaznamy[i].Time.Value).TotalSeconds == 1)
            {
                duration2++;
            }

            var coord1 = new GeoCoordinate(zaznamy[i].Latitude.Value, zaznamy[i].Longitude.Value);
            var coord2 = new GeoCoordinate(zaznamy[i + 1].Latitude.Value, zaznamy[i + 1].Longitude.Value);
            distance += coord1.GetDistanceTo(coord2);
            speeds2.Add(Math.Round(coord1.GetDistanceTo(coord2) * 3.6, 2));
            coordinates2.Add(new Coordinate(zaznamy[i].Longitude.Value, zaznamy[i].Latitude.Value));
        }
        
        //vykreslovanie trate do mapy
        LineString trackLineString = new (coordinates2.Select(v => SphericalMercator.FromLonLat(v.X, v.Y).ToCoordinate()).ToArray());
        ICollection<IStyle> styles = [
            new VectorStyle
            {
                Line = new Pen
                {
                    Color = Color.Red,
                    PenStrokeCap = PenStrokeCap.Butt,
                    StrokeJoin = StrokeJoin.Round,
                    Width = 5
                }
            }
        ];
        lineLayer = new MemoryLayer
        {
            Features =
            [
                new GeometryFeature
                {
                    Geometry = trackLineString,
                    Styles = styles
                }
            ],
            Name = "lineLayer",
        };
        
        lowerBound = 0;
        upperBound = zaznamy.Count - 1;
        
        //pridanie dat do ostatnych zoznamov
        elevations = zaznamy.Select(z=>Math.Round(z.Elevation.Value, 2)).ToList();
        heartrates = zaznamy.Select(z => z.HeartRate.Value).ToList();
        speeds = speeds2.ToList();
        temperatures = zaznamy.Select(z => z.Temperature.Value).ToList();
        times = zaznamy.Select(z => z.Time.Value.ToShortTimeString()).ToList();
        
        //nastavenie stavov vizualnych elementov
        RangeSlider.IsSnapToTickEnabled = true;
        RangeSlider.TickFrequency = 1;
        RangeSlider.Maximum = (int) zaznamy[zaznamy.Count - 1].Time.Value.Subtract(zaznamy[0].Time.Value).TotalSeconds;
        RangeSlider.LowerSelectedValue = 0;
        RangeSlider.UpperSelectedValue = (int) zaznamy[zaznamy.Count - 1].Time.Value.Subtract(zaznamy[0].Time.Value).TotalSeconds;
        LabelLower.Text = "Start: " + zaznamy[0].Time.Value.ToLongTimeString();
        LabelUpper.Text = "End: " + zaznamy[zaznamy.Count - 1].Time.Value.ToLongTimeString();
        TrackNameTextBlock.Text = name.InnerText;
        Map.Map.Layers.Add(Mapsui.Tiling.OpenStreetMap.CreateTileLayer());
        Map.Map.Layers.Add(lineLayer);
        MRect mRect = lineLayer.Features.First().Extent;
        mRect = mRect.Grow(1000);
        Map.Map.Home = n => n.ZoomToBox(mRect);
        Map.AddHandler(PointerWheelChangedEvent, OnMapPointerWheelChanged, RoutingStrategies.Bubble);
        ElevationChart.AddHandler(PointerWheelChangedEvent, OnChartPointerWheelChanged, RoutingStrategies.Tunnel);
        SpeedChart.AddHandler(PointerWheelChangedEvent, OnChartPointerWheelChanged, RoutingStrategies.Tunnel);
        TemperatureChart.AddHandler(PointerWheelChangedEvent, OnChartPointerWheelChanged, RoutingStrategies.Tunnel);
        HeartRateChart.AddHandler(PointerWheelChangedEvent, OnChartPointerWheelChanged, RoutingStrategies.Tunnel);
        
    }

    //metoda volajuca sa, ak sa posunie sliderom
    private void RangeSlider_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        reload(e);
    }
    
    //metoda meniaca stav okna
    private void reload(AvaloniaPropertyChangedEventArgs e)
    {
        //nastavenie noveho rozsahu, odkial pokial sa maju pocitat data
        if (e.Property == RangeBase.LowerSelectedValueProperty)
        {
            DateTime dateTime = zaznamy[0].Time.Value + TimeSpan.FromSeconds((int)RangeSlider.LowerSelectedValue);
            Zaznam zaznam = zaznamy[zaznamy.Count - 1];
            for (int i = 0; i < zaznamy.Count - 1; i++)
            {
                if (zaznamy[i + 1].Time.Value > dateTime)
                {
                    zaznam = zaznamy[i];
                    lowerBound = i;
                    break;
                }
            }
            LabelLower.Text = "Start: " + zaznam.Time.Value.ToLongTimeString();
        }
        else if (e.Property == RangeBase.UpperSelectedValueProperty)
        {
            DateTime dateTime = zaznamy[0].Time.Value + TimeSpan.FromSeconds((int)RangeSlider.UpperSelectedValue);
            Zaznam zaznam = zaznamy[zaznamy.Count - 1];
            for (int i = 0; i < zaznamy.Count - 1; i++)
            {
                if (zaznamy[i + 1].Time.Value > dateTime)
                {
                    zaznam = zaznamy[i];
                    upperBound = i;
                    break;
                }
            }
            LabelUpper.Text = "End: " + zaznam.Time.Value.ToLongTimeString();
        }

        if (e.Property == RangeBase.LowerSelectedValueProperty || e.Property == RangeBase.UpperSelectedValueProperty)
        {
            //vytvorenie lokalnych zoznamov pre dany rozsah
            List<double> elevations2 = elevations.GetRange(lowerBound, upperBound - lowerBound);
            List<double> speeds2 = speeds.GetRange(lowerBound, upperBound - lowerBound);
            List<double> temperatures2 = temperatures.GetRange(lowerBound, upperBound - lowerBound);
            List<double> heartrates2 = heartrates.GetRange(lowerBound, upperBound - lowerBound);
            List<string> times2 = times.GetRange(lowerBound, upperBound - lowerBound);
            List<Coordinate> coordinates = new List<Coordinate>();

            //optimalizacia pre dlhe trate - skreslenie vysledkov
            if (upperBound - lowerBound >= 1000)
            {
                List<double> elevations3 = new List<double>();
                List<double> speeds3 = new List<double>();
                List<double> temperatures3 = new List<double>();
                List<double> heartrates3 = new List<double>();
                List<string> times3 = new List<string>();
                for (int i = 0; i < upperBound - lowerBound; i = i + 10)
                {
                    elevations3.Add(elevations2[i]);
                    speeds3.Add(speeds2[i]);
                    temperatures3.Add(temperatures2[i]);
                    heartrates3.Add(heartrates2[i]);
                    times3.Add(times2[i]);
                }
                elevations2 = elevations3;
                speeds2 = speeds3;
                temperatures2 = temperatures3;
                heartrates2 = heartrates3;
                times2 = times3;
            }
            
            //vypocitanie priemernych hodnot
            double temp = 0;
            double hr = 0;
            double climb = 0;
            double descend = 0;
            int duration = 0;
            double distance = 0;
            int precision = 14;
            for (int i = lowerBound; i < upperBound - 1; i++)
            {
                
                if (zaznamy[i + 1].Time.Value.Subtract(zaznamy[i].Time.Value).TotalSeconds == 1)
                {
                    duration++;
                }

                var coord1 = new GeoCoordinate(zaznamy[i].Latitude.Value, zaznamy[i].Longitude.Value);
                var coord2 = new GeoCoordinate(zaznamy[i + 1].Latitude.Value, zaznamy[i + 1].Longitude.Value);
                distance += coord1.GetDistanceTo(coord2);
                
                if (i % precision == 0 && i <= upperBound - precision)
                {
                    
                    if (zaznamy[i].Elevation.Value < zaznamy[i + precision].Elevation.Value)
                    {
                        climb += zaznamy[i + precision].Elevation.Value - zaznamy[i].Elevation.Value;
                    }
                    else if (zaznamy[i].Elevation.Value > zaznamy[i + precision].Elevation.Value)
                    {
                        descend += zaznamy[i].Elevation.Value - zaznamy[i + precision].Elevation.Value;
                    }
                }
                
                coordinates.Add(new Coordinate(zaznamy[i].Longitude.Value, zaznamy[i].Latitude.Value));

                hr += zaznamy[i].HeartRate.Value;
                temp += zaznamy[i].Temperature.Value;
            }
            
            //aktualizacia vykreslovania trate do mapy
            LineString trackLineString = new (coordinates.Select(v => SphericalMercator.FromLonLat(v.X, v.Y).ToCoordinate()).ToArray());
            ICollection<IStyle> styles = [
                new VectorStyle
                {
                    Line = new Pen()
                    {
                        Color = Color.Red,
                        PenStrokeCap = PenStrokeCap.Butt,
                        StrokeJoin = StrokeJoin.Round,
                        Width = 5
                    }
                }
            ];
            MemoryLayer lineLayer = new MemoryLayer
            {
                Features =
                [
                    new GeometryFeature
                    {
                        Geometry = trackLineString,
                        Styles = styles
                    }
                ],
                Name = "lineLayer",
            };
            if (Map.Map.Layers.Count > 1)
            {
                var lastLayer = Map.Map.Layers[Map.Map.Layers.Count - 1];
                Map.Map.Layers.Remove(lastLayer);
            }
            Map.Map.Layers.Add(lineLayer);
            
            //osi grafov
            List<Axis> elevationChartXAxis = new List<Axis>
            {
                new Axis
                {
                    Labels = times2,
                }
            };
            
            List<Axis> invisibleAxis = new List<Axis>
            {
                new Axis
                {
                    IsVisible = false
                }
            };
            
            //naplnenie jednotlivych grafov
            ISeries[] elevationSeries = 
            [new LineSeries<double> 
            {
                Values = elevations2,
                Stroke = new SolidColorPaint(SKColors.CornflowerBlue),
                GeometryStroke = new SolidColorPaint(SKColors.CornflowerBlue),
                Fill = new SolidColorPaint(SKColors.SkyBlue),
                GeometrySize = 0,
            }];
            ElevationChart.Series = elevationSeries;
            ElevationChart.XAxes = elevationChartXAxis;
            ElevationChart.YAxes = invisibleAxis;
            
            ISeries[] temperatueSeries = 
            [new LineSeries<double> 
            {
                Values = temperatures2,
                Stroke = new SolidColorPaint(SKColors.Orange),
                GeometryStroke = new SolidColorPaint(SKColors.Orange),
                Fill = new SolidColorPaint(SKColors.LightYellow),
                GeometrySize = 0,
            }];
            TemperatureChart.Series = temperatueSeries;
            TemperatureChart.XAxes = elevationChartXAxis;
            TemperatureChart.YAxes = invisibleAxis;
            
            ISeries[] heartRateSeries = 
            [new LineSeries<double> 
            {
                Values = heartrates2,
                Stroke = new SolidColorPaint(SKColors.Red),
                GeometryStroke = new SolidColorPaint(SKColors.Red),
                Fill = new SolidColorPaint(SKColors.Pink),
                GeometrySize = 0,
            }];
            HeartRateChart.Series = heartRateSeries;
            HeartRateChart.XAxes = elevationChartXAxis;
            HeartRateChart.YAxes = invisibleAxis;
            
            ISeries[] speedSeries = 
            [new LineSeries<double> 
            {
                Values = speeds2,
                Stroke = new SolidColorPaint(SKColors.Green),
                GeometryStroke = new SolidColorPaint(SKColors.Green),
                Fill = new SolidColorPaint(SKColors.LightGreen),
                GeometrySize = 0,
            }];
            SpeedChart.Series = speedSeries;
            SpeedChart.XAxes = elevationChartXAxis;
            SpeedChart.YAxes = invisibleAxis;
            
            //vypocitanie poslednych hodnot a formatovanie
            TimeSpan t = TimeSpan.FromSeconds(duration);
            string duration2 = string.Format("{0:D2}h {1:D2}m {2:D2}s", 
                t.Hours, 
                t.Minutes, 
                t.Seconds);
            double speed = distance / duration * 3.6;
            temp = temp / (upperBound - lowerBound);
            hr = hr / (upperBound - lowerBound);

            //aktualizacia vizualnych elementov
            DistanceTextBlock.Text = "Distance: " + Math.Round(distance / 1000, 2) + " km";
            DurationTextBlock.Text = "Duration: " + duration2;
            AverageSpeedTextBlock.Text = "Average Speed: " + Math.Round(speed, 2) + " km/h";
            AverageTemperatureTextBlock.Text = "Average Temperature: " + Math.Round(temp, 2) + " C";
            AverageHeartRateTextBlock.Text = "Average HeartRate: " + Math.Round(hr, 2) + " BPM";
            TotalClimbTextBlock.Text = "Total Climb: " + Math.Round(climb, 2) + " m";
            TotalDescendTextBlock.Text = "Total Descend: " + Math.Round(descend, 2) + " m";
        }
    }

    //metoda zarucujuca, ze sa moze hybat hore a dole kolieckom, ked je kurzor na grafe
    private void OnChartPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = false;
        e.Route = RoutingStrategies.Bubble;
        StackPanel.RaiseEvent(e);
        e.Handled = true;
    }

    //nepouzity kod
    /*public static Action<T> Debounce<T>(Action<T> func, int milliseconds = 300)
    {
        var last = 0;
        return arg =>
        {
            var current = Interlocked.Increment(ref last);
            Task.Delay(milliseconds).ContinueWith(task =>
            {
                if (current == last)
                {
                    Dispatcher.UIThread.Invoke(()=>func(arg));
                }
                task.Dispose();
            });
        };
    }*/
    
    //metoda zarucujuca, ze sa nemoze hybat hore a dole kolieckom, ked je kurzor na mape
    private void OnMapPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;
    }
    
    
}