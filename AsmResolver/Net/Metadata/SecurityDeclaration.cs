﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using AsmResolver.Net.Builder;
using AsmResolver.Net.Signatures;

namespace AsmResolver.Net.Metadata
{
    public class SecurityDeclarationTable : MetadataTable<SecurityDeclaration>
    {
        public override MetadataTokenType TokenType
        {
            get { return MetadataTokenType.DeclSecurity; }
        }

        public override uint GetElementByteCount()
        {
            return sizeof (ushort) +
                   (uint)TableStream.GetIndexEncoder(CodedIndex.HasDeclSecurity).IndexSize +
                   (uint)TableStream.BlobIndexSize;
        }

        protected override SecurityDeclaration ReadMember(MetadataToken token, ReadingContext context)
        {
            var reader = context.Reader;
            return new SecurityDeclaration(Header, token, new MetadataRow<ushort, uint, uint>()
            {
                Column1 = reader.ReadUInt16(),
                Column2 = reader.ReadIndex(TableStream.GetIndexEncoder(CodedIndex.HasDeclSecurity).IndexSize),
                Column3 = reader.ReadIndex(TableStream.BlobIndexSize),
            });
        }

        protected override void UpdateMember(NetBuildingContext context, SecurityDeclaration member)
        {
            var row = member.MetadataRow;
            row.Column1 = (ushort)member.Action;
            row.Column2 = TableStream.GetIndexEncoder(CodedIndex.HasDeclSecurity)
                .EncodeToken(member.Parent.MetadataToken);
            row.Column3 = context.GetStreamBuffer<BlobStreamBuffer>().GetBlobOffset(member.PermissionSet);
        }

        protected override void WriteMember(WritingContext context, SecurityDeclaration member)
        {
            var writer = context.Writer;
            var row = member.MetadataRow;
            writer.WriteUInt16(row.Column1);
            writer.WriteIndex(TableStream.GetIndexEncoder(CodedIndex.HasDeclSecurity).IndexSize, row.Column2);
            writer.WriteIndex(TableStream.BlobIndexSize, row.Column3);
        }
    }

    public class SecurityDeclaration : MetadataMember<MetadataRow<ushort,uint,uint>>, IHasCustomAttribute
    {
        private readonly LazyValue<IHasSecurityAttribute> _parent;
        private readonly LazyValue<PermissionSetSignature> _permissionSet;
        private CustomAttributeCollection _customAttributes;

        public SecurityDeclaration(SecurityAction action, PermissionSetSignature permissionSet)
            : base(null, new MetadataToken(MetadataTokenType.DeclSecurity), new MetadataRow<ushort, uint, uint>())
        {
            Action = action;
            _parent = new LazyValue<IHasSecurityAttribute>();
            _permissionSet = new LazyValue<PermissionSetSignature>(permissionSet);
        }

        internal SecurityDeclaration(MetadataHeader header, MetadataToken token, MetadataRow<ushort, uint, uint> row)
            : base(header, token, row)
        {
            Action = (SecurityAction)row.Column1;

            var tableStream = header.GetStream<TableStream>();

            _parent = new LazyValue<IHasSecurityAttribute>(() =>
            {
                var parentToken = tableStream.GetIndexEncoder(CodedIndex.HasDeclSecurity).DecodeIndex(row.Column2);
                return parentToken.Rid != 0 ? (IHasSecurityAttribute)tableStream.ResolveMember(parentToken) : null;
            });

            _permissionSet = new LazyValue<PermissionSetSignature>(() => 
                PermissionSetSignature.FromReader(header, header.GetStream<BlobStream>().CreateBlobReader(row.Column3)));
        }

        public IHasSecurityAttribute Parent
        {
            get { return _parent.Value; }
            set { _parent.Value = value; }
        }

        public SecurityAction Action
        {
            get;
            set;
        }

        public PermissionSetSignature PermissionSet
        {
            get { return _permissionSet.Value; }
            set { _permissionSet.Value = value; }
        }

        public CustomAttributeCollection CustomAttributes
        {
            get
            {
                if (_customAttributes != null)
                    return _customAttributes;
                return _customAttributes = new CustomAttributeCollection(this);
            }
        }
    }
}
