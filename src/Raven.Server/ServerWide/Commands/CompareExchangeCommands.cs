﻿using System;
using System.IO;
using Raven.Server.ServerWide.Context;
using Sparrow.Json;
using Sparrow.Json.Parsing;
using Voron;
using Voron.Data.Tables;

namespace Raven.Server.ServerWide.Commands
{
    public abstract class CompareExchangeCommandBase : CommandBase
    {
        public string Key;
        public long Index;

        protected CompareExchangeCommandBase(){ }

        protected CompareExchangeCommandBase(string key, long index)
        {
            if(string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key),"The key argument must have value");
            if(index < 0)
                throw new InvalidDataException("Index must be a non-negative number");

            Key = key;
            Index = index;
        }

        public abstract (long Index, object Value) Execute(TransactionOperationContext context, Table items, long index);
        
        public override DynamicJsonValue ToJson(JsonOperationContext context)
        {
            var json = base.ToJson(context);
            json[nameof(Key)] = Key;
            json[nameof(Index)] = Index;
            return json;
        }
    }

    public class RemoveCompareExchangeCommand : CompareExchangeCommandBase
    {
        public RemoveCompareExchangeCommand(){}
        public RemoveCompareExchangeCommand(string key, long index) : base(key, index){}
        
        public override unsafe (long Index, object Value) Execute(TransactionOperationContext context, Table items, long index)
        {
            var dbKey = Key.ToLowerInvariant();
            using (Slice.From(context.Allocator, dbKey, out Slice keySlice))
            {
                if (items.ReadByKey(keySlice, out var reader))
                {
                    var itemIndex = *(long*)reader.Read((int)ClusterStateMachine.UniqueItems.Index, out var _);
                    var storeValue = reader.Read((int)ClusterStateMachine.UniqueItems.Value, out var size);
                    var result = new BlittableJsonReaderObject(storeValue, size, context);
                    if (Index == itemIndex)
                    {
                        result = result.Clone(context);
                        items.Delete(reader.Id);
                        return (index, result);
                    }
                    return (itemIndex, result);
                }
            }
            return (index, null);
        }
    }
    
    public class AddOrUpdateCompareExchangeCommand : CompareExchangeCommandBase
    {
        public BlittableJsonReaderObject Value;

        public AddOrUpdateCompareExchangeCommand(){}
        
        public AddOrUpdateCompareExchangeCommand(string key, BlittableJsonReaderObject value, long index) : base(key, index)
        {
            Value = value;
        }

        public override unsafe (long Index, object Value) Execute(TransactionOperationContext context, Table items, long index)
        {
            var dbKey = Key.ToLowerInvariant();
            Value = Value.Clone(context);
            long itemIndex;
            using (Slice.From(context.Allocator, dbKey, out Slice keySlice))
            using (items.Allocate(out TableValueBuilder tvb))
            {
                tvb.Add(keySlice.Content.Ptr, keySlice.Size);
                tvb.Add(index);
                tvb.Add(Value.BasePointer, Value.Size);

                if (items.ReadByKey(keySlice, out var reader))
                {
                    itemIndex = *(long*)reader.Read((int)ClusterStateMachine.UniqueItems.Index, out var _);
                    if (Index == itemIndex)
                    {
                        items.Update(reader.Id, tvb);
                    }
                    else
                    {
                        // concurrency violation, so we return the current value
                        return (itemIndex, new BlittableJsonReaderObject(reader.Read((int)ClusterStateMachine.UniqueItems.Value, out var size), size, context));
                    }
                }
                else
                {
                    items.Set(tvb);
                }
            }
            return (index, Value);
        }

        public override DynamicJsonValue ToJson(JsonOperationContext context)
        {
            var json = base.ToJson(context);
            json[nameof(Value)] = Value;
            return json;
        }
    }
}
