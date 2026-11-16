#if false
using Netcode;
using System;
using System.IO;

namespace GameSource.Network
{
  public class IncomingMessage : IDisposable
  {
    private byte messageType;
    private long farmerID;
    private byte[] data;
    private MemoryStream stream;
    private BinaryReader reader;

    public byte MessageType => this.messageType;

    public long FarmerID => this.farmerID;

    public Farmer SourceFarmer => Game1.getFarmer(this.farmerID);

    public byte[] Data => this.data;

    public BinaryReader Reader => this.reader;

    public void Read(BinaryReader reader)
    {
      this.Dispose();
      this.messageType = reader.ReadByte();
      this.farmerID = reader.ReadInt64();
      this.data = reader.ReadSkippableBytes();
      this.stream = new MemoryStream(this.data);
      this.reader = new BinaryReader((Stream) this.stream);
    }

    public void Dispose()
    {
      if (this.reader != null)
        this.reader.Dispose();
      if (this.stream != null)
        this.stream.Dispose();
      this.stream = (MemoryStream) null;
      this.reader = (BinaryReader) null;
    }
  }
}
#endif
