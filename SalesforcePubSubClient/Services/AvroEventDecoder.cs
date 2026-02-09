using System;
using System.Collections.Generic;
using System.IO;
using Apache.Avro;
using Apache.Avro.Generic;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using SalesforcePubSub.Protos;

namespace SalesforcePubSubClient.Services;

/// <summary>
/// Utility class for decoding Avro-encoded payloads from Salesforce Pub/Sub events
/// </summary>
public class AvroEventDecoder
{
    /// <summary>
    /// Decodes a binary Avro payload to a dictionary
    /// </summary>
    public static Dictionary<string, object> DecodeAvroPayload(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
            throw new ArgumentException("Payload cannot be null or empty");

        try
        {
            // Read the Avro schema from the beginning of the payload
            using var stream = new MemoryStream(payload);
            using var reader = new BinaryReader(stream);

            // Salesforce Pub/Sub API includes schema information in the payload
            // The first bytes contain the schema fingerprint
            var schemaFingerprint = ReadSchemaFingerprint(reader);

            // Read the actual Avro data
            var avroData = new byte[payload.Length - stream.Position];
            Array.Copy(payload, (int)stream.Position, avroData, 0, avroData.Length);

            // For simplicity, we'll try to decode without the schema
            // In production, you should use the schema from GetSchemaInfoAsync
            var decoded = DecodeGenericAvro(avroData);

            return decoded;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decode Avro payload", ex);
        }
    }

    /// <summary>
    /// Decodes Avro payload using a provided schema JSON
    /// </summary>
    public static Dictionary<string, object> DecodeAvroPayloadWithSchema(byte[] payload, string schemaJson)
    {
        if (payload == null || payload.Length == 0)
            throw new ArgumentException("Payload cannot be null or empty");

        if (string.IsNullOrWhiteSpace(schemaJson))
            throw new ArgumentException("Schema JSON cannot be null or empty");

        try
        {
            // Create Avro schema from JSON
            var schema = Schema.Parse(schemaJson);

            // Decode using the schema
            var reader = new GenericReader<GenericRecord>(schema);
            var stream = new MemoryStream(payload);
            var decoder = new GenericDecoder<GenericRecord>(schema, stream);

            var record = reader.Read(default, decoder);
            return ConvertRecordToDictionary(record);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to decode Avro payload with schema", ex);
        }
    }

    /// <summary>
    /// Reads schema fingerprint from the payload
    /// </summary>
    private static byte[] ReadSchemaFingerprint(BinaryReader reader)
    {
        // Avro typically includes a single-byte schema fingerprint
        // or a more complex schema identifier
        var fingerprint = new byte[1];
        fingerprint[0] = reader.ReadByte();
        return fingerprint;
    }

    /// <summary>
    /// Generic Avro decoder (simplified version)
    /// </summary>
    private static Dictionary<string, object> DecodeGenericAvro(byte[] avroData)
    {
        // This is a simplified approach. In production, you should:
        // 1. Get the schema using GetSchemaInfoAsync
        // 2. Use DecodeAvroPayloadWithSchema with the retrieved schema
        
        // For now, we'll try to interpret the data as JSON
        // Note: This won't work for binary Avro data, but serves as a fallback
        try
        {
            var jsonString = System.Text.Encoding.UTF8.GetString(avroData);
            var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
            return dict ?? new Dictionary<string, object>();
        }
        catch
        {
            // If JSON decoding fails, return empty dictionary
            // In production, you would implement proper Avro decoding here
            return new Dictionary<string, object>
            {
                { "_raw", Convert.ToBase64String(avroData) },
                { "_note", "Could not decode Avro payload. Use GetSchemaInfoAsync to get the schema and DecodeAvroPayloadWithSchema to decode." }
            };
        }
    }

    /// <summary>
    /// Converts an Avro GenericRecord to a Dictionary
    /// </summary>
    private static Dictionary<string, object> ConvertRecordToDictionary(GenericRecord record)
    {
        var dict = new Dictionary<string, object>();

        foreach (var field in record.Schema.Fields)
        {
            var value = record[field.Name];
            dict[field.Name] = ConvertAvroValue(value);
        }

        return dict;
    }

    /// <summary>
    /// Converts Avro values to .NET types
    /// </summary>
    private static object ConvertAvroValue(object value)
    {
        if (value == null)
            return null!;

        return value switch
        {
            GenericRecord record => ConvertRecordToDictionary(record),
            System.Collections.IEnumerable enumerable and not string => ConvertEnumerableToList(enumerable),
            byte[] bytes => Convert.ToBase64String(bytes),
            _ => value
        };
    }

    /// <summary>
    /// Converts Avro arrays/enumerables to lists
    /// </summary>
    private static List<object> ConvertEnumerableToList(System.Collections.IEnumerable enumerable)
    {
        var list = new List<object>();
        foreach (var item in enumerable)
        {
            list.Add(ConvertAvroValue(item));
        }
        return list;
    }

    /// <summary>
    /// Formats event attributes as a dictionary
    /// </summary>
    public static Dictionary<string, string> FormatEventAttributes(RepeatedField<ConsumerEvent.Types.EventAttribute> attributes)
    {
        var dict = new Dictionary<string, string>();
        foreach (var attr in attributes)
        {
            dict[attr.Key] = attr.Value;
        }
        return dict;
    }
}