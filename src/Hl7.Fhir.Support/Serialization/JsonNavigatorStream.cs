﻿/* 
 * Copyright (c) 2017, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://github.com/ewoutkramer/fhir-net-api/blob/master/LICENSE
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hl7.Fhir.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Hl7.Fhir.ElementModel;
using System.Collections;

#if NET_FILESYSTEM

namespace Hl7.Fhir.Serialization
{
    /// <summary>
    /// Provides efficient extraction of summary information from a raw FHIR JSON resource file,
    /// without actually deserializing the full resource. Also supports resource bundles.
    /// </summary>
    /// <remarks>Replacement for JsonArtifactScanner (now obsolete).</remarks>
    public class JsonNavigatorStream : INavigatorStream
    {
        private readonly FileStream _fileStream = null;
        private JsonReader _reader = null;
        private (JObject element, string fullUrl)? _current = null;

        public JsonNavigatorStream(string path)
        {
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _reader = SerializationUtil.JsonReaderFromStream(_fileStream);

            Reset();
        }

        #region IDisposable

        bool _disposed;

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_reader != null)
                    {
                        ((IDisposable)_reader).Dispose();
                        _reader = null;
                    }

                    if (_fileStream != null)
                    {
                        _fileStream.Dispose();
                        // _fileStream = null;
                    }
                }

                // release any unmanaged objects
                // set the object references to null

                _disposed = true;
            }
        }

        #endregion

        /// <summary>The typename of the underlying resource (container).</summary>
        /// <remarks>Call Current.Type to determine the type of the currently enumerated resource.</remarks>
        public string ResourceType { get; private set; }

        /// <summary>The full path of the current resource file, or of the containing resource bundle file.</summary>
        public string Path => _fileStream?.Name;

        /// <summary>Returns <c>true</c> if the underlying file represents a Bundle resource, or <c>false</c> otherwise.</summary>
        public bool IsBundle => ResourceType == "Bundle";

        public void Reset()
        {
            throwIfDisposed();

            _fileStream.Seek(0, SeekOrigin.Begin);
            _reader = SerializationUtil.JsonReaderFromStream(_fileStream);

            ResourceType = scanForResourceType(_reader);

            // Reset - again, since getrootName may have found the resource type at the end of the file
            _fileStream.Seek(0, SeekOrigin.Begin);
            _reader = SerializationUtil.JsonReaderFromStream(_fileStream);

            // In a Bundle, try to move to first entry - which really is our first resource.
            // We ignore the result, MoveNext() will correctly return false if searching here fails.
            if (IsBundle) skipTo(_reader, "entry");
        }

        public bool MoveNext() => MoveNext(null);

        public bool MoveNext(string fullUrl)
        {
            throwIfDisposed();
            if (ResourceType == null) return false;

            if (IsBundle)
            {
                while (_reader.Read())
                {
                    if (_reader.TokenType == JsonToken.StartObject && _reader.Path.StartsWith("entry["))
                    {
                        if (skipTo(_reader, "fullUrl"))
                        {
                            var entryUrl = _reader.ReadAsString();
                            if (entryUrl != null && (fullUrl == null || entryUrl == fullUrl))
                            {
                                if (skipTo(_reader, "resource") && _reader.Read())
                                {
                                    var resourceNode = (JObject)JObject.ReadFrom(_reader);
                                    _current = (resourceNode, entryUrl);
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }

            else
            {
                if (_reader.TokenType != JsonToken.EndObject)
                {
                    var resource = (JObject)JObject.ReadFrom(_reader);

                    if (resource != null)
                    {
                        // First try to initialize from canonical url (conformance resources)
                        var canonicalUrl = resource.Value<string>("url");

                        // Otherwise try to initialize from resource id
                        if (canonicalUrl == null)
                        {
                            // [WMR 20171016] Note: ResourceType property returns container type (e.g. Bundle)
                            // But here we need the type of the *current* entry
                            // Q: Should we call scanForResourceType() ?
                            //    Inefficient; must reset/recreate reader afterwards...
                            var resType = resource.Value<string>("resourceType");

                            var resourceId = resource.Value<string>("id");
                            if (resourceId != null)
                            {
                                canonicalUrl = "http://example.org/" + resType + "/" + resourceId;
                            }
                        }

                        if (canonicalUrl != null && (fullUrl == null || canonicalUrl == fullUrl))
                            _current = (resource, canonicalUrl);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Seek(string position)
        {
            throwIfDisposed();
            if (position == null) throw Error.ArgumentNull(nameof(position));

            // start looking from the beginning
            Reset();

            return MoveNext(position);

            //This code needs to be moved to the DirectorySource - which now assumes the streamer
            //does this (but no longer - as we don't return Resources anymore)
            //var resultResource = new FhirXmlParser().Parse<Resource>(new XmlDomFhirReader(found));
            //resultResource.SetOrigin(entry.Origin);
            //return resultResource
        }

        public string Position => _current?.fullUrl;

        /// <summary>Returns a new <see cref="IElementNavigator"/> instance positioned on the current entry.</summary>
        public IElementNavigator Current
        {
            get
            {
                throwIfDisposed();
                var jelem = _current?.element;
                if (jelem != null)
                    return JsonDomFhirNavigator.Create(jelem);
                else
                    return null;
            }
        }

        object IEnumerator.Current => this.Current;

        #region private helpers

        string scanForResourceType(JsonReader reader) => skipTo(reader, "resourceType") ? reader.ReadAsString() : null;

        static bool skipTo(JsonReader reader, string path)
        {
            // Throws on invalid input
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName && reader.Value.Equals(path))
                    return true;
            }
            return false;
        }

        void throwIfDisposed()
        {
            if (_disposed) { throw new ObjectDisposedException(GetType().FullName); }
        }

        #endregion

    }
}

#endif