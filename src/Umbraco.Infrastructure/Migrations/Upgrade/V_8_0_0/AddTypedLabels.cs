﻿using System;
using System.Globalization;
using System.Linq;
using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.Dtos;
using Umbraco.Extensions;

namespace Umbraco.Cms.Infrastructure.Migrations.Upgrade.V_8_0_0
{
    public class AddTypedLabels : MigrationBase
    {
        public AddTypedLabels(IMigrationContext context)
            : base(context)
        { }

        protected override void Migrate()
        {
            // insert other label datatypes

            void InsertNodeDto(int id, int sortOrder, string uniqueId, string text)
            {
                var nodeDto = new NodeDto
                {
                    NodeId = id,
                    Trashed = false,
                    ParentId = -1,
                    UserId = -1,
                    Level = 1,
                    Path = "-1,-" + id,
                    SortOrder = sortOrder,
                    UniqueId = new Guid(uniqueId),
                    Text = text,
                    NodeObjectType = Cms.Core.Constants.ObjectTypes.DataType,
                    CreateDate = DateTime.Now
                };

                Database.Insert(Cms.Core.Constants.DatabaseSchema.Tables.Node, "id", false, nodeDto);
            }

            if (SqlSyntax.SupportsIdentityInsert())
                Database.Execute(new Sql($"SET IDENTITY_INSERT {SqlSyntax.GetQuotedTableName(Cms.Core.Constants.DatabaseSchema.Tables.Node)} ON "));

            InsertNodeDto(Cms.Core.Constants.DataTypes.LabelInt, 36, "8e7f995c-bd81-4627-9932-c40e568ec788", "Label (integer)");
            InsertNodeDto(Cms.Core.Constants.DataTypes.LabelBigint, 36, "930861bf-e262-4ead-a704-f99453565708", "Label (bigint)");
            InsertNodeDto(Cms.Core.Constants.DataTypes.LabelDateTime, 37, "0e9794eb-f9b5-4f20-a788-93acd233a7e4", "Label (datetime)");
            InsertNodeDto(Cms.Core.Constants.DataTypes.LabelTime, 38, "a97cec69-9b71-4c30-8b12-ec398860d7e8", "Label (time)");
            InsertNodeDto(Cms.Core.Constants.DataTypes.LabelDecimal, 39, "8f1ef1e1-9de4-40d3-a072-6673f631ca64", "Label (decimal)");

            if (SqlSyntax.SupportsIdentityInsert())
                Database.Execute(new Sql($"SET IDENTITY_INSERT {SqlSyntax.GetQuotedTableName(Cms.Core.Constants.DatabaseSchema.Tables.Node)} OFF "));

            void InsertDataTypeDto(int id, string dbType, string? configuration = null)
            {
                var dataTypeDto = new DataTypeDto
                {
                    NodeId = id,
                    EditorAlias = Cms.Core.Constants.PropertyEditors.Aliases.Label,
                    DbType = dbType
                };

                if (configuration != null)
                    dataTypeDto.Configuration = configuration;

                Database.Insert(Cms.Core.Constants.DatabaseSchema.Tables.DataType, "pk", false, dataTypeDto);
            }

            InsertDataTypeDto(Cms.Core.Constants.DataTypes.LabelInt, "Integer", "{\"umbracoDataValueType\":\"INT\"}");
            InsertDataTypeDto(Cms.Core.Constants.DataTypes.LabelBigint, "Nvarchar", "{\"umbracoDataValueType\":\"BIGINT\"}");
            InsertDataTypeDto(Cms.Core.Constants.DataTypes.LabelDateTime, "Date", "{\"umbracoDataValueType\":\"DATETIME\"}");
            InsertDataTypeDto(Cms.Core.Constants.DataTypes.LabelDecimal, "Decimal", "{\"umbracoDataValueType\":\"DECIMAL\"}");
            InsertDataTypeDto(Cms.Core.Constants.DataTypes.LabelTime, "Date", "{\"umbracoDataValueType\":\"TIME\"}");

            // flip known property types

            var labelPropertyTypes = Database.Fetch<PropertyTypeDto>(Sql()
                .Select<PropertyTypeDto>(x => x.Id, x => x.Alias)
                .From<PropertyTypeDto>()
                .Where<PropertyTypeDto>(x => x.DataTypeId == Cms.Core.Constants.DataTypes.LabelString));

            var intPropertyAliases = new[] { Cms.Core.Constants.Conventions.Media.Width, Cms.Core.Constants.Conventions.Media.Height, Cms.Core.Constants.Conventions.Member.FailedPasswordAttempts };
            var bigintPropertyAliases = new[] { Cms.Core.Constants.Conventions.Media.Bytes };
            var dtPropertyAliases = new[] { Cms.Core.Constants.Conventions.Member.LastLockoutDate, Cms.Core.Constants.Conventions.Member.LastLoginDate, Cms.Core.Constants.Conventions.Member.LastPasswordChangeDate };

            var intPropertyTypes = labelPropertyTypes.Where(pt => intPropertyAliases.Contains(pt.Alias)).Select(pt => pt.Id).ToArray();
            var bigintPropertyTypes = labelPropertyTypes.Where(pt => bigintPropertyAliases.Contains(pt.Alias)).Select(pt => pt.Id).ToArray();
            var dtPropertyTypes = labelPropertyTypes.Where(pt => dtPropertyAliases.Contains(pt.Alias)).Select(pt => pt.Id).ToArray();

            Database.Execute(Sql().Update<PropertyTypeDto>(u => u.Set(x => x.DataTypeId, Cms.Core.Constants.DataTypes.LabelInt)).WhereIn<PropertyTypeDto>(x => x.Id, intPropertyTypes));
            Database.Execute(Sql().Update<PropertyTypeDto>(u => u.Set(x => x.DataTypeId, Cms.Core.Constants.DataTypes.LabelInt)).WhereIn<PropertyTypeDto>(x => x.Id, intPropertyTypes));
            Database.Execute(Sql().Update<PropertyTypeDto>(u => u.Set(x => x.DataTypeId, Cms.Core.Constants.DataTypes.LabelBigint)).WhereIn<PropertyTypeDto>(x => x.Id, bigintPropertyTypes));
            Database.Execute(Sql().Update<PropertyTypeDto>(u => u.Set(x => x.DataTypeId, Cms.Core.Constants.DataTypes.LabelDateTime)).WhereIn<PropertyTypeDto>(x => x.Id, dtPropertyTypes));

            // update values for known property types
            // depending on the size of the site, that *may* take time
            // but we want to parse in C# not in the database
            var values = Database.Fetch<PropertyDataValue>(Sql()
                .Select<PropertyDataDto>(x => x.Id, x => x.VarcharValue)
                .From<PropertyDataDto>()
                .WhereIn<PropertyDataDto>(x => x.PropertyTypeId, intPropertyTypes));
            foreach (var value in values)
                Database.Execute(Sql()
                    .Update<PropertyDataDto>(u => u
                        .Set(x => x.IntegerValue, string.IsNullOrWhiteSpace(value.VarcharValue) ? (int?)null : int.Parse(value.VarcharValue, NumberStyles.Any, CultureInfo.InvariantCulture))
                        .Set(x => x.TextValue, null)
                        .Set(x => x.VarcharValue, null))
                    .Where<PropertyDataDto>(x => x.Id == value.Id));

            values = Database.Fetch<PropertyDataValue>(Sql().Select<PropertyDataDto>(x => x.Id, x => x.VarcharValue).From<PropertyDataDto>().WhereIn<PropertyDataDto>(x => x.PropertyTypeId, dtPropertyTypes));
            foreach (var value in values)
                Database.Execute(Sql()
                    .Update<PropertyDataDto>(u => u
                        .Set(x => x.DateValue, string.IsNullOrWhiteSpace(value.VarcharValue) ? (DateTime?)null : DateTime.Parse(value.VarcharValue, CultureInfo.InvariantCulture, DateTimeStyles.None))
                        .Set(x => x.TextValue, null)
                        .Set(x => x.VarcharValue, null))
                    .Where<PropertyDataDto>(x => x.Id == value.Id));

            // anything that's custom... ppl will have to figure it out manually, there isn't much we can do about it
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        private class PropertyDataValue
        {
            public int Id { get; set; }
            public string? VarcharValue { get;set; }
        }
        // ReSharper restore UnusedAutoPropertyAccessor.Local
    }
}
