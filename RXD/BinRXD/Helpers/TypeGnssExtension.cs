using RXD.Blocks;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace RXD.Helpers
{
    

    public static class TypeGnssExtension
    {
        public static string ToDisplayName(this TypeGNSS type)
        {
            switch (type)
            {
                case TypeGNSS.LATITUDE: return "Latitude";
                case TypeGNSS.LONGITUDE: return "Longitude";
                case TypeGNSS.ALTITUDE: return "Altitude";
                case TypeGNSS.DATETIME: return "Date/Time";
                case TypeGNSS.SPEED_OVER_GROUND: return "Speed over ground";
                case TypeGNSS.GROUND_DISTANCE: return "Ground distance";
                case TypeGNSS.COURSE_OVER_GROUND: return "Course over ground";
                case TypeGNSS.GEOID_SEPARATION: return "Geoid separation";
                case TypeGNSS.NUMBER_SATELLITES: return "Number of satellites";
                case TypeGNSS.QUALITY: return "Quality";
                case TypeGNSS.HORIZONTAL_ACCURACY: return "Horizontal accuracy";
                case TypeGNSS.VERTICAL_ACCURACY: return "Vertical accuracy";
                case TypeGNSS.SPEED_ACCURACY: return "Speed accuracy";
                case TypeGNSS.VEHICLE_ROLL: return "Vehicle Roll";
                case TypeGNSS.VEHICLE_PITCH: return "Vehicle Pitch";
                case TypeGNSS.VEHICLE_HEADING: return "Vehicle Heading";
                case TypeGNSS.VEHICLE_ROLL_ACCURACY: return "Vehicle Roll Accuracy";
                case TypeGNSS.VEHICLE_PITCH_ACCURACY: return "Vehicle Pitch Accuracy";
                case TypeGNSS.VEHICLE_HEADING_ACCURACY: return "Vehicle Heading Accuracy";
                case TypeGNSS.ACCELERATION_X: return "Acceleration X";
                case TypeGNSS.ACCELERATION_Y: return "Acceleration Y";
                case TypeGNSS.ACCELERATION_Z: return "Acceleration Z";
                case TypeGNSS.ANGULAR_RATE_X: return "Angular Rate X";
                case TypeGNSS.ANGULAR_RATE_Y: return "Angular Rate Y";
                case TypeGNSS.ANGULAR_RATE_Z: return "Angular Rate Z";
                case TypeGNSS.GEOFENCE_1: return "Geofence 1";
                case TypeGNSS.GEOFENCE_2: return "Geofence 2";
                case TypeGNSS.GEOFENCE_3: return "Geofence 3";
                case TypeGNSS.GEOFENCE_4: return "Geofence 4";
                case TypeGNSS.GNSS_TIMESTAMP: return "GNSS Timestamp";
                default: return type.ToString();
            }
        }        
    }
}
