using System;

namespace AvaloniaApplication1;

//objekt, v ktorom sa ukladaju informacie o kazdom zazname nacitanom z .gpx suboru
public class Zaznam
{
	public double? Elevation { get; set; }
	public double? Latitude { get; set; }
	public double? Longitude { get; set; }
	public DateTime? Time { get; set; }
	public double? Temperature { get; set; }
	public double? HeartRate { get; set; }
}