using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace RXD.Blocks
{
    public enum BlockType : UInt16
    {
        Unknown = 0,
        Config = 1,
        CANInterface = 2,
        CANMessage = 3,
        CANSignal = 4,
        Constant = 5,
        Counter = 6,
        Arithmetic = 7,
        Rational_Formula = 8,
        Condition = 9,
        ADC = 10,
        Switch = 11,
        Custom = 12,
        SDInterface = 13,
        SDMessage = 14,
        CANError = 15,
        USBInterface = 16,
        USBMessage = 17,
        Trigger = 18,
        VariableAggregation = 19,
        Timer = 20,
        Digital_Input = 21,
        Digital_Output = 22,
        J1939DM = 23,
        Accelerometer = 24,
        Gyro = 25,
        GNSSInterface = 26,
        GNSSMessage = 27,
        INTERNAL_PARAMETER = 28,
        Config_Ftp = 29,
        Config_Mobile = 30,
        LED = 31,
        LINInterface = 32,
        LINMessage = 33,
        CONFIG_S3 = 34,
        DAQFILE = 35,
        DAQ = 36,
        DAQITEM = 37,
        TC = 38,
        LINSignal = 39,
        Config_WiFi = 40,
        UDS = 41,
        Relational_Operator = 42,
    };

    public enum RecordType : byte
    {
        Unknown,
        PreBuffer,
        Event,
        CanTrace,
        CanError,
        LinTrace,
        MessageData,
    }

    public abstract partial class BinBase
    {
        /// <summary>
        /// BlockType to Class reference dictionary
        /// </summary>
        public static readonly Dictionary<BlockType, Type> BlockInfo = new Dictionary<BlockType, Type>()
        {
            { BlockType.Unknown, typeof(BinBase) },

            // Config blocks
            { BlockType.Config, typeof(BinConfig) },
            { BlockType.Config_Ftp, typeof(BinConfigFTP) },
            { BlockType.Config_Mobile, typeof(BinConfigMobile) },
            { BlockType.Config_WiFi, typeof(BinConfigWiFi) },

            // Data blocks
            { BlockType.CANInterface, typeof(BinCanInterface) },
            { BlockType.CANMessage, typeof(BinCanMessage) },
            { BlockType.CANSignal, typeof(BinCanSignal) },
            { BlockType.Constant, typeof(BinConstant) },
            { BlockType.Counter, typeof(BinCounter) },
            { BlockType.Arithmetic, typeof(BinArithmetic) },
            { BlockType.Rational_Formula, typeof(BinRationalFormula) },
            { BlockType.Condition, typeof(BinCondition) },
            { BlockType.ADC, typeof(BinADC) },
            { BlockType.Switch, typeof(BinSwitch) },
            { BlockType.Custom, typeof(BinCustom) },
            { BlockType.SDInterface, typeof(BinSDInterface) },
            { BlockType.SDMessage, typeof(BinSDMessage) },
            { BlockType.CANError, typeof(BinCanError) },
            { BlockType.USBInterface, typeof(BinUsbInterface) },
            { BlockType.USBMessage, typeof(BinUsbMessage) },
            { BlockType.Trigger, typeof(BinTrigger) },
            { BlockType.VariableAggregation, typeof(BinVariableAggregation) },
            { BlockType.Timer, typeof(BinTimer) },
            { BlockType.Digital_Input, typeof(BinDigitalIn) },
            { BlockType.Digital_Output, typeof(BinDigitalOut) },
            { BlockType.J1939DM, typeof(BinJ1939DM) },
            { BlockType.Accelerometer, typeof(BinAccelerometer) },
            { BlockType.Gyro, typeof(BinGyro) },
            { BlockType.GNSSInterface, typeof(BinGNSSInterface) },
            { BlockType.GNSSMessage, typeof(BinGNSSMessage) },
            { BlockType.INTERNAL_PARAMETER, typeof(BinInternalParameter) },
            { BlockType.LED, typeof(BinLEDStatus) },
            { BlockType.LINInterface, typeof(BinLinInterface) },
            { BlockType.LINMessage, typeof(BinLinMessage) },
            { BlockType.CONFIG_S3, typeof(BinConfigS3) },
            { BlockType.DAQFILE, typeof(BinDaqFile) },
            { BlockType.DAQ, typeof(BinDAQ) },
            { BlockType.DAQITEM, typeof(BinDAQItem) },
            { BlockType.TC, typeof(BinTC) },
            { BlockType.LINSignal, typeof(BinLinSignal) },
            { BlockType.UDS, typeof(BinUDS) },
            { BlockType.Relational_Operator, typeof(BinRelationalOperator) },
        };

        /// <summary>
        /// BlockType to RecordType reference dictionary
        /// </summary>
        internal static readonly Dictionary<BlockType, RecordType> RecordInfo = new Dictionary<BlockType, RecordType>()
        {
            { BlockType.Unknown, RecordType.Unknown },

            // Config blocks
            { BlockType.Config, RecordType.Unknown },
            { BlockType.Config_Ftp, RecordType.Unknown },
            { BlockType.Config_Mobile, RecordType.Unknown },
            { BlockType.CONFIG_S3, RecordType.Unknown },
            { BlockType.Config_WiFi, RecordType.Unknown },

            // Data blocks
            { BlockType.CANInterface, RecordType.Unknown },
            { BlockType.CANMessage, RecordType.CanTrace },
            { BlockType.CANSignal, RecordType.MessageData },
            { BlockType.Constant, RecordType.Unknown },
            { BlockType.Counter, RecordType.Unknown },
            { BlockType.Arithmetic, RecordType.Unknown },
            { BlockType.Rational_Formula, RecordType.Unknown },
            { BlockType.Condition, RecordType.Unknown },
            { BlockType.ADC, RecordType.MessageData },
            { BlockType.Switch, RecordType.Unknown },
            { BlockType.Custom, RecordType.Unknown },
            { BlockType.SDInterface, RecordType.Unknown },
            { BlockType.SDMessage, RecordType.CanTrace },
            { BlockType.CANError, RecordType.CanError },
            { BlockType.USBInterface, RecordType.Unknown },
            { BlockType.USBMessage, RecordType.Unknown },
            { BlockType.Trigger, RecordType.PreBuffer },
            { BlockType.VariableAggregation, RecordType.Unknown },
            { BlockType.Timer, RecordType.Unknown },
            { BlockType.Digital_Input, RecordType.MessageData },
            { BlockType.Digital_Output, RecordType.Unknown },
            { BlockType.J1939DM, RecordType.Unknown },
            { BlockType.Accelerometer, RecordType.MessageData },
            { BlockType.Gyro, RecordType.MessageData },
            { BlockType.GNSSInterface, RecordType.Unknown },
            { BlockType.GNSSMessage, RecordType.MessageData },
            { BlockType.INTERNAL_PARAMETER, RecordType.Unknown },
            { BlockType.LED, RecordType.Unknown },
            { BlockType.LINInterface, RecordType.Unknown },
            { BlockType.LINMessage, RecordType.LinTrace },
            { BlockType.DAQFILE, RecordType.Unknown },
            { BlockType.DAQ, RecordType.Unknown },
            { BlockType.DAQITEM, RecordType.Unknown },
            { BlockType.TC, RecordType.Unknown },
            { BlockType.LINSignal, RecordType.MessageData },
            { BlockType.UDS, RecordType.Unknown },
            { BlockType.Relational_Operator, RecordType.Unknown },


        };

        protected static void ChecksumUpdate(byte[] buffer, ref byte crc)
        {
            foreach (byte b in buffer)
                crc += b;
        }

        internal static BinBase ReadNext(BinaryReader br)
        {
            byte crc = 0;

            // Read Header
            byte[] buffer = br.ReadBytes(Marshal.SizeOf(typeof(BinHeader)));
            BinHeader hs = BinHeader.ReadBlock(buffer);
            if (BlockInfo.TryGetValue(hs.type, out Type binType))
            {
                if (binType.Equals(typeof(BinBase)))
                    return null;

                BinBase block = (BinBase)Activator.CreateInstance(binType, hs);
                if (block is BinConfig && hs.version < 4)
                {
                    Array.Resize(ref buffer, buffer.Length - Marshal.SizeOf(hs.uniqueid));
                    br.BaseStream.Seek(-2, SeekOrigin.Current);
                }
                ChecksumUpdate(buffer, ref crc);

                buffer = br.ReadBytes(hs.length - buffer.Length - 1);
                ChecksumUpdate(buffer, ref crc);
                byte fcrc = br.ReadByte();
                if (fcrc != crc)
                    return null;

                // Parse data
                block.data.Parse(buffer);

                return block;
            }
            return null;
        }

    }
}
