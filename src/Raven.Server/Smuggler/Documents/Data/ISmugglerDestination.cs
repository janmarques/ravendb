﻿using System;
using System.IO;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Smuggler;
using Raven.Server.Documents;
using Raven.Server.Documents.Indexes;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;

namespace Raven.Server.Smuggler.Documents.Data
{
    public interface ISmugglerDestination
    {
        IDisposable Initialize(DatabaseSmugglerOptions options, SmugglerResult result, long buildVersion);
        IDocumentActions Documents();
        IDocumentActions RevisionDocuments();
        IDocumentActions Tombstones();
        IDocumentActions Conflicts();
        IIndexActions Indexes();
        IKeyValueActions<long> Identities();
        IKeyValueActions<BlittableJsonReaderObject> CmpXchg();
    }

    public interface IDocumentActions : INewDocumentActions, IDisposable
    {
        void WriteDocument(DocumentItem item, SmugglerProgressBase.CountsWithLastEtag progress);
        void WriteTombstone(DocumentTombstone tombstone, SmugglerProgressBase.CountsWithLastEtag progress);
        void WriteConflict(DocumentConflict conflict, SmugglerProgressBase.CountsWithLastEtag progress);
    }

    public interface INewDocumentActions
    {
        DocumentsOperationContext GetContextForNewDocument();
        Stream GetTempStream();
    }

    public interface IIndexActions : IDisposable
    {
        void WriteIndex(IndexDefinitionBase indexDefinition, IndexType indexType);
        void WriteIndex(IndexDefinition indexDefinition);
    }

    public interface IKeyValueActions<in T> : IDisposable
    {
        void WriteKeyValue(string key, T value);
    }
}
