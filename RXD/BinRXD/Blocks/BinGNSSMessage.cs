using InfluxShared.FileObjects;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace RXD.Blocks
{
    #region Enumerations for Property type definitions
    public enum TypeGNSS : byte
    {
        LATITUDE,
        LONGITUDE,
        ALTITUDE,
        DATETIME,
        SPEED_OVER_GROUND,
        GROUND_DISTANCE,
        COURSE_OVER_GROUND,
        GEOID_SEPARATION,
        NUMBER_SATELLITES,
        QUALITY,
        HORIZONTAL_ACCURACY,
        VERTICAL_ACCURACY,
        SPEED_ACCURACY,
        VEHICLE_ROLL,
        VEHICLE_PITCH,
        VEHICLE_HEADING,
        VEHICLE_ROLL_ACCURACY,
        VEHICLE_PITCH_ACCURACY,
        VEHICLE_HEADING_ACCURACY,
        ACCELERATION_X,
        ACCELERATION_Y,
        ACCELERATION_Z,
        ANGULAR_RATE_X,
        ANGULAR_RATE_Y,
        ANGULAR_RATE_Z,
        GEOFENCE_1,
        GEOFENCE_2,
        GEOFENCE_3,
        GEOFENCE_4,
        GNSS_TIMESTAMP
    }



    #endregion

    public class BinGNSSMessage : BinBase
    {

        internal enum BinProp
        {
            InterfaceUID,
            Type,
            InputUID
        }

        #region Do not touch these
        public BinGNSSMessage(BinHeader hs = null) : base(hs) { }

        internal dynamic this[BinProp index]
        {
            get => data.GetProperty(index.ToString());
            set => data.SetProperty(index.ToString(), value);
        }
        #endregion

        

        internal static Dictionary<TypeGNSS, Type> GnssType = new()
        {
            { TypeGNSS.LATITUDE, typeof(Double) },
            { TypeGNSS.LONGITUDE, typeof(Double) },
            { TypeGNSS.ALTITUDE, typeof(Single) },
            { TypeGNSS.DATETIME, typeof(UInt32) },
            { TypeGNSS.SPEED_OVER_GROUND, typeof(Single) },
            { TypeGNSS.GROUND_DISTANCE, typeof(Single) },
            { TypeGNSS.COURSE_OVER_GROUND, typeof(Single) },
            { TypeGNSS.GEOID_SEPARATION, typeof(Single) },
            { TypeGNSS.NUMBER_SATELLITES, typeof(Single) },
            { TypeGNSS.QUALITY, typeof(Single) },
            { TypeGNSS.HORIZONTAL_ACCURACY, typeof(Single) },
            { TypeGNSS.VERTICAL_ACCURACY, typeof(Single) },
            { TypeGNSS.SPEED_ACCURACY, typeof(Single) },
            { TypeGNSS.VEHICLE_ROLL, typeof(Single) },
            { TypeGNSS.VEHICLE_PITCH, typeof(Single) },
            { TypeGNSS.VEHICLE_HEADING, typeof(Single) },
            { TypeGNSS.VEHICLE_ROLL_ACCURACY, typeof(Single) },
            { TypeGNSS.VEHICLE_PITCH_ACCURACY, typeof(Single) },
            { TypeGNSS.VEHICLE_HEADING_ACCURACY, typeof(Single) },
            { TypeGNSS.ACCELERATION_X, typeof(Single) },
            { TypeGNSS.ACCELERATION_Y, typeof(Single) },
            { TypeGNSS.ACCELERATION_Z, typeof(Single) },
            { TypeGNSS.ANGULAR_RATE_X, typeof(Single) },
            { TypeGNSS.ANGULAR_RATE_Y, typeof(Single) },
            { TypeGNSS.ANGULAR_RATE_Z, typeof(Single) },
            { TypeGNSS.GEOFENCE_1, typeof(Single) },
            { TypeGNSS.GEOFENCE_2, typeof(Single) },
            { TypeGNSS.GEOFENCE_3, typeof(Single) },
            { TypeGNSS.GEOFENCE_4, typeof(Single) } ,
            { TypeGNSS.GNSS_TIMESTAMP, typeof(UInt32) }
    };

        public override string GetName => this[BinProp.Type].ToString();
        //internal override string GetUnits => "";
        public override ChannelDescriptor GetDataDescriptor => new()
        {
            StartBit = 0,
            BitCount = (ushort)(8 * Marshal.SizeOf(GnssType[this[BinGNSSMessage.BinProp.Type]] as Type)),
            isIntel = true,
            HexType = GnssType[this[BinGNSSMessage.BinProp.Type]],
            conversionType = ConversionType.None,
            Name = GetName,
            Units = GetUnits
        };

        internal override void SetupVersions()
        {
            Versions[1] = new Action(() =>
            {
                data.AddProperty(BinProp.InterfaceUID, typeof(UInt16));
                data.AddProperty(BinProp.Type, typeof(TypeGNSS));

                AddInput(BinProp.InterfaceUID.ToString());
            });
            Versions[2] = new Action(() =>
            {
                Versions[1].DynamicInvoke();
                data.AddProperty(BinProp.InputUID, typeof(UInt16));
            });
        }
    }
}
