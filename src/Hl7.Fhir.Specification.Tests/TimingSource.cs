﻿/* 
 * Copyright (c) 2016, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Source.Summary;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Hl7.Fhir.Specification.Tests
{
    public partial class SnapshotGeneratorTest
    {
        class TimingSource : IConformanceSource
        {
            IConformanceSource _source;
            TimeSpan _duration = TimeSpan.Zero;

            public TimingSource(IConformanceSource source) { _source = source; }

            public IEnumerable<ArtifactSummary> ListSummaries() => throw new NotImplementedException();

            public IEnumerable<ConceptMap> FindConceptMaps(string sourceUri = null, string targetUri = null)
                => measureDuration(() => _source.FindConceptMaps(sourceUri, targetUri));

            public NamingSystem FindNamingSystem(string uniqueid) => measureDuration(() => _source.FindNamingSystem(uniqueid));

            public CodeSystem FindCodeSystemByValueSet(string system) => measureDuration(() => _source.FindCodeSystemByValueSet(system));

            public IEnumerable<string> ListResourceUris(ResourceType? filter = default(ResourceType?)) => _source.ListResourceUris(filter);
            // => measureDuration(() => _source.ListResourceUris(filter));

            public Resource ResolveByCanonicalUri(string uri) => measureDuration(() => _source.ResolveByCanonicalUri(uri));

            public Resource ResolveByUri(string uri) => measureDuration(() => _source.ResolveByUri(uri));

            T measureDuration<T>(Func<T> f)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var result = f();
                sw.Stop();
                _duration += sw.Elapsed;
                return result;
            }

            public TimeSpan Duration => _duration;

            public void Reset() { _duration = TimeSpan.Zero; }

            public void ShowDuration(int count, TimeSpan totalDuration)
            {
                var totalMs = totalDuration.TotalMilliseconds;
                var resolverMs = _duration.TotalMilliseconds;
                var resolverFraction = resolverMs / totalMs;
                var snapshotMs = totalMs - resolverMs;
                var snapshotFraction = snapshotMs / totalMs;
                // Debug.Print($"Generated {count} snapshots in {totalMs} ms = {sourceMs} ms (resolver) + {snapshotMs} (snapshot) ({perc:2}%), on average {avg} ms per snapshot.");
                Console.WriteLine($"Generated {count} snapshots in {totalMs} ms = {resolverMs} ms (resolver) ({resolverFraction:P0}) + {snapshotMs} (snapshot) ({snapshotFraction:P0}).");
                var totalAvg = totalMs / count;
                var resolverAvg = resolverMs / count;
                var snapshotAvg = snapshotMs / count;
                Console.WriteLine($"Average per resource: {totalAvg} = {resolverAvg} ms (resolver) + {snapshotAvg} ms (snapshot)");
            }

        }
    }
}

