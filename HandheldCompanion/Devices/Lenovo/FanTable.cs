using System;
using System.IO;

namespace HandheldCompanion.Devices.Lenovo
{
    public readonly struct FanTable
    {
        // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
        // ReSharper disable MemberCanBePrivate.Global
        // ReSharper disable IdentifierTypo
        // ReSharper disable InconsistentNaming

        public byte FSTM { get; init; }
        public byte FSID { get; init; }
        public uint FSTL { get; init; }
        public ushort FSS0 { get; init; }
        public ushort FSS1 { get; init; }
        public ushort FSS2 { get; init; }
        public ushort FSS3 { get; init; }
        public ushort FSS4 { get; init; }
        public ushort FSS5 { get; init; }
        public ushort FSS6 { get; init; }
        public ushort FSS7 { get; init; }
        public ushort FSS8 { get; init; }
        public ushort FSS9 { get; init; }

        // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
        // ReSharper restore MemberCanBePrivate.Global
        // ReSharper restore IdentifierTypo
        // ReSharper restore InconsistentNaming

        public FanTable(ushort[] fanTable)
        {
            if (fanTable.Length != 10)
                // ReSharper disable once LocalizableElement
                throw new ArgumentException("Fan table length must be 10.", nameof(fanTable));

            FSTM = 1;
            FSID = 0;
            FSTL = 0;
            FSS0 = fanTable[0];
            FSS1 = fanTable[1];
            FSS2 = fanTable[2];
            FSS3 = fanTable[3];
            FSS4 = fanTable[4];
            FSS5 = fanTable[5];
            FSS6 = fanTable[6];
            FSS7 = fanTable[7];
            FSS8 = fanTable[8];
            FSS9 = fanTable[9];
        }

        public ushort[] GetTable() => new[] { FSS0, FSS1, FSS2, FSS3, FSS4, FSS5, FSS6, FSS7, FSS8, FSS9 };

        public byte[] GetBytes()
        {
            using var ms = new MemoryStream(new byte[64]);
            ms.WriteByte(FSTM);
            ms.WriteByte(FSID);
            ms.Write(BitConverter.GetBytes(FSTL));
            ms.Write(BitConverter.GetBytes(FSS0));
            ms.Write(BitConverter.GetBytes(FSS1));
            ms.Write(BitConverter.GetBytes(FSS2));
            ms.Write(BitConverter.GetBytes(FSS3));
            ms.Write(BitConverter.GetBytes(FSS4));
            ms.Write(BitConverter.GetBytes(FSS5));
            ms.Write(BitConverter.GetBytes(FSS6));
            ms.Write(BitConverter.GetBytes(FSS7));
            ms.Write(BitConverter.GetBytes(FSS8));
            ms.Write(BitConverter.GetBytes(FSS9));
            return ms.ToArray();
        }

        public override string ToString() =>
            $"{nameof(FSTM)}: {FSTM}," +
            $" {nameof(FSID)}: {FSID}," +
            $" {nameof(FSTL)}: {FSTL}," +
            $" {nameof(FSS0)}: {FSS0}," +
            $" {nameof(FSS1)}: {FSS1}," +
            $" {nameof(FSS2)}: {FSS2}," +
            $" {nameof(FSS3)}: {FSS3}," +
            $" {nameof(FSS4)}: {FSS4}," +
            $" {nameof(FSS5)}: {FSS5}," +
            $" {nameof(FSS6)}: {FSS6}," +
            $" {nameof(FSS7)}: {FSS7}," +
            $" {nameof(FSS8)}: {FSS8}," +
            $" {nameof(FSS9)}: {FSS9}";
    }
}
