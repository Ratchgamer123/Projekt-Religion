using Mirror;
using FirstGearGames.Utilities.Networks;
using UnityEngine;

namespace FirstGearGames.Mirrors.Assets.FlexNetworkTransforms
{
    /// <summary>
    /// Data received on server from clients when using Client Authoritative movement.
    /// </summary>
    public class ReceivedClientData
    {
        #region Types.
        public enum DataTypes
        {
            Interval = 0,
            Teleport = 1
        }
        #endregion
        public ReceivedClientData() { }
        public ReceivedClientData(DataTypes dataType, bool localSpace, TransformSyncData data)
        {
            DataType = dataType;
            LocalSpace = localSpace;
            Data = data;
        }

        public DataTypes DataType;
        public bool LocalSpace;
        public TransformSyncData Data;
    }

    [System.Serializable, System.Flags]
    public enum Axes : int
    {
        X = 1,
        Y = 2,
        Z = 4
    }

    [System.Flags]
    public enum CompressedAxes : byte
    {
        None = 0,
        XPositive = 1,
        XNegative = 2,
        YPositive = 4,
        YNegative = 8,
        ZPositive = 16,
        ZNegative = 32
    }

    /// <summary>
    /// Transform properties which need to be synchronized.
    /// </summary>
    [System.Flags]
    public enum SyncProperties : byte
    {
        None = 0,
        //Position included.
        Position = 1,
        //Rotation included.
        Rotation = 2,
        //Scale included.
        Scale = 4,
        //Indicates transform did not move.
        Settled = 8,
        //Indicates a networked platform is included.
        Platform = 16,
        //Indicates to compress small values.
        CompressSmall = 32,
        //Indicates a compression level.
        Id1 = 64,
        //Indicates a compression level.
        Id2 = 128
    }


    /// <summary>
    /// Using strongly typed for performance.
    /// </summary>
    public static class EnumContains
    {
        /// <summary>
        /// Returns if a CompressedAxes Whole contains Part.
        /// </summary>
        /// <param name="whole"></param>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool CompressedAxesContains(CompressedAxes whole, CompressedAxes part)
        {
            return (whole & part) == part;
        }
        /// <summary>
        /// Returns if a SyncProperties Whole contains Part.
        /// </summary>
        /// <param name="whole"></param>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool SyncPropertiesContains(SyncProperties whole, SyncProperties part)
        {
            return (whole & part) == part;
        }
        /// <summary>
        /// Returns if a Axess Whole contains Part.
        /// </summary>
        /// <param name="whole"></param>
        /// <param name="part"></param>
        /// <returns></returns>
        public static bool AxesContains(Axes whole, Axes part)
        {
            return (whole & part) == part;
        }
    }


    /// <summary>
    /// Container holding latest transform values.
    /// </summary>
    [System.Serializable]
    public class TransformSyncData
    {
        public TransformSyncData() { }
        public void UpdateValues(byte syncProperties, uint networkIdentity, byte componentIndex, Vector3 position, Quaternion rotation, Vector3 scale, uint platformNetId)
        {
            SyncProperties = syncProperties;
            NetworkIdentity = networkIdentity;
            ComponentIndex = componentIndex;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            PlatformNetId = platformNetId;
        }

        public byte SyncProperties;
        public uint NetworkIdentity;
        public byte ComponentIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public uint PlatformNetId = 0;
    }

    public static class FlexNetworkTransformSerializers
    {
        private const float MAX_COMPRESSION_VALUE = 654f;
        /// <summary>
        /// Writes TransformSyncData into a writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="syncData"></param>
        public static void WriteTransformSyncData(this NetworkWriter writer, TransformSyncData syncData)
        {
            //SyncProperties.
            SyncProperties sp = (SyncProperties)syncData.SyncProperties;
            writer.WriteByte(syncData.SyncProperties);

            //NetworkIdentity.
            //Get compression level for netIdentity.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Id1))
                writer.WriteByte((byte)syncData.NetworkIdentity);
            else if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Id2))
                writer.WriteUInt16((ushort)syncData.NetworkIdentity);
            else
                writer.WriteUInt32(syncData.NetworkIdentity);
            //ComponentIndex.
            writer.WriteByte(syncData.ComponentIndex);

            //Position.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Position))
            {
                if (EnumContains.SyncPropertiesContains(sp, SyncProperties.CompressSmall))
                    WriteCompressedVector3(writer, syncData.Position);
                else
                    writer.WriteVector3(syncData.Position);
            }
            //Rotation.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Rotation))
                writer.WriteUInt32(Quaternions.CompressQuaternion(syncData.Rotation));
            //Scale.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Scale))
            {
                if (EnumContains.SyncPropertiesContains(sp, SyncProperties.CompressSmall))
                    WriteCompressedVector3(writer, syncData.Scale);
                else
                    writer.WriteVector3(syncData.Scale);
            }
            //Platform.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Platform))
                writer.WriteUInt32(syncData.PlatformNetId);
        }

        /// <summary>
        /// Converts reader data into a new TransformSyncData.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static TransformSyncData ReadTransformSyncData(this NetworkReader reader)
        {
            TransformSyncData syncData = new TransformSyncData();

            //Sync properties.
            SyncProperties sp = (SyncProperties)reader.ReadByte();
            syncData.SyncProperties = (byte)sp;

            //NetworkIdentity.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Id1))
                syncData.NetworkIdentity = reader.ReadByte();
            else if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Id2))
                syncData.NetworkIdentity = reader.ReadUInt16();
            else
                syncData.NetworkIdentity = reader.ReadUInt32();
            //ComponentIndex.
            syncData.ComponentIndex = reader.ReadByte();

            //Position.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Position))
            {
                if (EnumContains.SyncPropertiesContains(sp, SyncProperties.CompressSmall))
                    syncData.Position = ReadCompressedVector3(reader);
                else
                    syncData.Position = reader.ReadVector3();
            }
            //Rotation.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Rotation))
                syncData.Rotation = Quaternions.DecompressQuaternion(reader.ReadUInt32());
            //scale.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Scale))
            {
                if (EnumContains.SyncPropertiesContains(sp, SyncProperties.CompressSmall))
                    syncData.Scale = ReadCompressedVector3(reader);
                else
                    syncData.Scale = reader.ReadVector3();
            }
            //Platformed.
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Platform))
                syncData.PlatformNetId = reader.ReadUInt32();

            return syncData;
        }

        /// <summary>
        /// Writes a compressed Vector3 to the writer.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="ca"></param>
        /// <param name="v"></param>
        private static void WriteCompressedVector3(NetworkWriter writer, Vector3 v)
        {
            CompressedAxes ca = CompressedAxes.None;
            //If can compress X.
            float absX = Mathf.Abs(v.x);
            if (absX <= MAX_COMPRESSION_VALUE)
                ca |= (Mathf.Sign(v.x) > 0f) ? CompressedAxes.XPositive : CompressedAxes.XNegative;
            //If can compress Y.
            float absY = Mathf.Abs(v.y);
            if (absY <= MAX_COMPRESSION_VALUE)
                ca |= (Mathf.Sign(v.y) > 0f) ? CompressedAxes.YPositive : CompressedAxes.YNegative;
            //If can compress Z.
            float absZ = Mathf.Abs(v.z);
            if (absZ <= MAX_COMPRESSION_VALUE)
                ca |= (Mathf.Sign(v.z) > 0f) ? CompressedAxes.ZPositive : CompressedAxes.ZNegative;

            //Write compresed axes.
            writer.WriteByte((byte)ca);
            //X
            if (EnumContains.CompressedAxesContains(ca, CompressedAxes.XNegative) || EnumContains.CompressedAxesContains(ca, CompressedAxes.XPositive))
                writer.WriteUInt16((ushort)Mathf.Round(absX * 100f));
            else
                writer.WriteSingle(v.x);
            //Y
            if (EnumContains.CompressedAxesContains(ca, CompressedAxes.YNegative) || EnumContains.CompressedAxesContains(ca, CompressedAxes.YPositive))
                writer.WriteUInt16((ushort)Mathf.Round(absY * 100f));
            else
                writer.WriteSingle(v.y);
            //Z
            if (EnumContains.CompressedAxesContains(ca, CompressedAxes.ZNegative) || EnumContains.CompressedAxesContains(ca, CompressedAxes.ZPositive))
                writer.WriteUInt16((ushort)Mathf.Round(absZ * 100f));
            else
                writer.WriteSingle(v.z);
        }


        /// <summary>
        /// Reads a compressed Vector3.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="ca"></param>
        /// <param name="v"></param>
        private static Vector3 ReadCompressedVector3(NetworkReader reader)
        {
            CompressedAxes ca = (CompressedAxes)reader.ReadByte();
            //Sign of compressed axes. If 0f, no compression was used for the axes.
            float sign;

            //X
            float x;
            if (EnumContains.CompressedAxesContains(ca, CompressedAxes.XNegative))
                sign = -1f;
            else if (EnumContains.CompressedAxesContains(ca, CompressedAxes.XPositive))
                sign = 1f;
            else
                sign = 0f;
            //If there is compression.
            if (sign != 0f)
                x = (reader.ReadUInt16() / 100f) * sign;
            else
                x = reader.ReadSingle();

            //Y
            float y;
            if (EnumContains.CompressedAxesContains(ca, CompressedAxes.YNegative))
                sign = -1f;
            else if (EnumContains.CompressedAxesContains(ca, CompressedAxes.YPositive))
                sign = 1f;
            else
                sign = 0f;
            //If there is compression.
            if (sign != 0f)
                y = (reader.ReadUInt16() / 100f) * sign;
            else
                y = reader.ReadSingle();

            //Z
            float z;
            if (EnumContains.CompressedAxesContains(ca, CompressedAxes.ZNegative))
                sign = -1f;
            else if (EnumContains.CompressedAxesContains(ca, CompressedAxes.ZPositive))
                sign = 1f;
            else
                sign = 0f;
            //If there is compression.
            if (sign != 0f)
                z = (reader.ReadUInt16() / 100f) * sign;
            else
                z = reader.ReadSingle();

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Returns if a Vector3 can be compressed.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool CanCompressVector3(ref Vector3 v)
        {
            return
                (v.x > -MAX_COMPRESSION_VALUE && v.x < MAX_COMPRESSION_VALUE) ||
                (v.y > -MAX_COMPRESSION_VALUE && v.y < MAX_COMPRESSION_VALUE) ||
                (v.z > -MAX_COMPRESSION_VALUE && v.z < MAX_COMPRESSION_VALUE);
        }
    }


}