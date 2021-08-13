/*
 * Copyright (C) 2021 Nicholas Orlowsky 
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Nicholas Orlowsky <nicholasorlowsky@gmail.com>
 */

using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace RoomyAPI
{
   /// <summary>
    /// Location in the real world. Powered by Google Maps
    /// </summary>
    public class Location
    {
        /// <summary>
        /// The street address of the location. Usually numbers & the street name
        /// </summary>
        [MaxLength(100)]
        [MinLength(1)]
        public string StreetAddress { get; set; }
        
        /// <summary>
        /// The state of the location
        /// </summary>
        [MaxLength(100)]
        [MinLength(1)]
        public string State { get; set; }
        
        /// <summary>
        /// The country of the location
        /// </summary>
        [MaxLength(100)]
        [MinLength(1)]
        public string Country { get; set; }
        
        /// <summary>
        /// The city of the location
        /// </summary>
        [MaxLength(100)]
        [MinLength(1)]
        public string City { get; set; }
        
        /// <summary>
        /// The Zip code of the location
        /// </summary>
        [MaxLength(100)]
        [MinLength(1)]
        public string Zip { get; set; }
        
        /// <summary>
        /// The Google Maps code for the location to be used with cool signup flows
        /// </summary>
        [MaxLength(100)]
        [MinLength(1)]
        public string GoogleLocationId { get; set; }
        
        public double Longitude { get; set; }
        public double Latitude { get; set; }

        /// <summary>
        /// Gets a string representation of the location
        /// </summary>
        /// <returns>An address in the form of a string that conforms to normal address standards</returns>
        public override string ToString()
        {
            return $"{StreetAddress} {City}, {State} {Country} {Zip}";
        }

        public static async Task<Location> ParseFromId(string id)
        {
            try
            {
                string googleUrl = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={id}&key="+RoomyEnv.Google.MapsAPIKey;
                string content = FileGetContents(googleUrl);
                JObject o = JObject.Parse(content);
                Location l =  await Parse(o);
                l.GoogleLocationId = id;
                return l;
            }
            catch (Exception e)
            {
                throw new Exception();// TODO
            }
        }

        private static async Task<Location> Parse(JObject o)
        {
            Location l = new Location();
            // So you're here
            // this is by no doubt terribly written, and I wouldn't be surprised if you thought i'm stupid
            // if you don't do it with a loop it just doesn't work
            // hours have been spent here
            // it works, just leave it

            string switchval = "";
            try
            {
                o.SelectToken("result.address_components[0].types[0]").ToString();
                switchval = "result.";
            }
            catch (Exception e)
            {
                switchval = "results[0].";
            }
            
            int i = 0;
            while (i != -1)
            {
                // I know this looks bad just trust me don't try to fix it 
                switch (o.SelectToken(switchval+"address_components[" + i + "].types[0]").ToString())
                {
                    case "street_number":
                        l.StreetAddress = o.SelectToken(switchval+"address_components[" + i + "].short_name")
                            .ToString();
                        break;
                    case "route":
                        l.StreetAddress +=
                            " " + o.SelectToken(switchval+"address_components[" + i + "].short_name").ToString();
                        break;
                    case "locality":
                        l.City = o.SelectToken(switchval+"address_components[" + i + "].short_name").ToString();
                        break;
                    case "administrative_area_level_1":
                        l.State = o.SelectToken(switchval+"address_components[" + i + "].short_name").ToString();
                        break;
                    case "country":
                        l.Country = o.SelectToken(switchval+"address_components[" + i + "].short_name").ToString();
                        break;
                    case "postal_code":
                        l.Zip = o.SelectToken(switchval+"address_components[" + i + "].short_name").ToString();
                        i = -2;
                        break;
                }

                i++;
            }

            l.Latitude = (double) o.SelectToken(switchval + "geometry.location.lat");
            l.Longitude = (double) o.SelectToken(switchval + "geometry.location.lng");

            return l;
        }

        /// <summary>
        /// Gets the location of a string address
        /// </summary>
        /// <param name="s">The string representation of the location</param>
        /// <returns></returns>
        /// <exception cref="InvalidAddressException">The address is not recognized by google maps</exception>
        public static async Task<Location> Parse(string s)
        {
            try
            {
                string googleUrl = "https://maps.googleapis.com/maps/api/geocode/json?address=" + s +
                                   "&key="+RoomyEnv.Google.MapsAPIKey;
                string content = FileGetContents(googleUrl);
                JObject o = JObject.Parse(content);
                return await Parse(o);
            }
            catch
            {
                throw new Exception(); //TODO
            }
        }


        /// <summary>
        /// Gets information for the Google Maps API Call
        /// </summary>
        /// <param name="fileName">idk i stole this off of SO 2 years ago</param>
        /// <returns>see above</returns>
        private static string FileGetContents(string fileName)
        {
            string sContents = string.Empty;
                string me = string.Empty;
                try
                {
                    if (fileName.ToLower().IndexOf("https:") > -1)
                    {
                        System.Net.WebClient wc = new System.Net.WebClient();
                        byte[] response = wc.DownloadData(fileName);
                        sContents = System.Text.Encoding.ASCII.GetString(response);

                    }
                    else
                    {
                        System.IO.StreamReader sr = new System.IO.StreamReader(fileName);
                        sContents = sr.ReadToEnd();
                        sr.Close();
                    }
                }
                catch { sContents = "unable to connect to server "; }
                return sContents;
            }

    }

   /// <summary>
   /// Extra props that we throw in the ol' db
   /// </summary>
   public class LocationReport : Location
   {
       public DateTime ReportDateTime { get; set; }
       public Guid Id { get; set; }
   }
}