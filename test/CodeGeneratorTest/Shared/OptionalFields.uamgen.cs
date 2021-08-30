//------------------------------------------------------------------------------
// <auto-generated>
//     Generated by MSBuild generator.
//     Source: OptionalFields.cs
// </auto-generated>
//------------------------------------------------------------------------------

using GodSharp.Extensions.Opc.Ua.Types;
using Opc.Ua;
using static GodSharp.Extensions.Opc.Ua.Types.Encodings.EncodingFactory;

namespace CodeGeneratorTest
{
	public partial class OptionalFields : ComplexObject
	{
		public uint EncodingMask;

		public override void Encode(IEncoder encoder)
		{
			base.Encode(encoder);
			encoder.WriteUInt32("EncodingMask",EncodingMask);
			Encoding.Write(encoder,MandatoryInt32, nameof(MandatoryInt32));
			if ((1 & EncodingMask) != 0)
			{
				Encoding.Write(encoder,OptionalInt32, nameof(OptionalInt32));
			}
			Encoding.Write(encoder,MandatoryStringArray, nameof(MandatoryStringArray));
			if ((2 & EncodingMask) != 0)
			{
				Encoding.Write(encoder,OptionalStringArray, nameof(OptionalStringArray));
			}
		}

		public override void Decode(IDecoder decoder)
		{
			base.Decode(decoder);
			EncodingMask = decoder.ReadUInt32("EncodingMask");
			Encoding.Read(decoder,ref MandatoryInt32, nameof(MandatoryInt32));
			if ((1 & EncodingMask) != 0)
			{
				Encoding.Read(decoder,ref OptionalInt32, nameof(OptionalInt32));
			}
			Encoding.Read(decoder,ref MandatoryStringArray, nameof(MandatoryStringArray));
			if ((2 & EncodingMask) != 0)
			{
				Encoding.Read(decoder,ref OptionalStringArray, nameof(OptionalStringArray));
			}
		}
	}
}
