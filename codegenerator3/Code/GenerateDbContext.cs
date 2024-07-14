using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Entity;
using System.IO;

/*
 notes on error: Possibly unhandled rejection:
 as soon as you work with the .$promise, you have to include another .catch() 
 if you don't, then you get the error. 
     */

namespace WEB.Models
{
    public partial class Code
    {
        public string GenerateDbContext()
        {
            var calculatedFields = AllEntities.SelectMany(e => e.Fields).Where(f => f.EditPageType == EditPageType.CalculatedField);

            var s = new StringBuilder();

            s.Add($"using Microsoft.EntityFrameworkCore;");
            s.Add($"");
            s.Add($"namespace {CurrentEntity.Project.Namespace}.Models");
            s.Add($"{{");
            s.Add($"    public partial class ApplicationDbContext");
            s.Add($"    {{");
            foreach (var e in NormalEntities)
            {
                s.Add($"        public DbSet<{e.Name}> {e.PluralName} {{ get; set; }}");

                if (e.Fields.Any(f => f.EditPageType == EditPageType.FileContents && !f.UseAzureBlobStorage))
                    s.Add($"        public DbSet<{e.Name}Content> {e.Name}Contents {{ get; set; }}");

            }
            s.Add($"");
            s.Add($"        public void ConfigureModelBuilder(ModelBuilder modelBuilder)");
            s.Add($"        {{");
            //foreach (var field in calculatedFields)
            //{
            //    s.Add($"            modelBuilder.Entity<{field.Entity.Name}>().Ignore(t => t.{field.Name});");
            //}
            //if (calculatedFields.Count() > 0) s.Add($"");

            foreach (var relationship in DbContext.Relationships
                .Include(o => o.ParentEntity)
                .Include(o => o.ChildEntity)
                .Include(o => o.RelationshipFields.Select(p => p.ChildField))
               .Where(o => o.IsOneToOne && o.ParentEntity.ProjectId == CurrentEntity.ProjectId))
            {
                s.Add($"            modelBuilder.Entity<{relationship.ParentEntity.Name}>()");
                s.Add($"                .HasOne(o => o.{relationship.ChildEntity.Name})");
                s.Add($"                .WithOne(o => o.{relationship.ParentEntity.Name})");
                s.Add($"                .HasForeignKey<{relationship.ChildEntity.Name}>(o => o.{relationship.RelationshipFields.First().ChildField.Name});");
                s.Add($"");
            }


            foreach (var entity in AllEntities.OrderBy(o => o.Name))
            {
                var needsBreak = false;
                if (entity.KeyFields.Count > 1)
                {
                    s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                    s.Add($"                .HasKey(o => new {{ {entity.KeyFields.Select(o => "o." + o.Name).Aggregate((current, next) => current + ", " + next)} }})");
                    s.Add($"                .HasName(\"PK_{entity.Name}\");");
                    needsBreak = true;
                }

                foreach (var field in entity.Fields.Where(o => !o.IsNullable && o.IsUnique).OrderBy(f => f.FieldOrder))
                {
                    if (field.IsUniqueOnHierarchy)
                    {
                        var rel = entity.RelationshipsAsChild.SingleOrDefault(r => r.Hierarchy);
                        if (rel == null) throw new Exception($"Field has IsUniqueOnHierarchy but no hierarchy: {entity.Name}.{field.Name}");
                        s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                        s.Add($"                .HasIndex(o => new {{ o.{rel.RelationshipFields.First().ChildField.Name}, o.{field.Name} }})");
                        s.Add($"                .HasDatabaseName(\"IX_{entity.Name}_{field.Name}\")");
                        s.Add($"                .IsUnique();");
                        needsBreak = true;
                    }
                    else if (field.IsUnique)
                    {
                        s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                        s.Add($"                .HasIndex(o => o.{field.Name})");
                        s.Add($"                .HasDatabaseName(\"IX_{entity.Name}_{field.Name}\")");
                        s.Add($"                .IsUnique();");
                        needsBreak = true;
                    }
                }

                if (needsBreak) s.Add($"");

                var fileContentsField = entity.Fields.FirstOrDefault(o => o.EditPageType == EditPageType.FileContents);
                if (fileContentsField != null && !fileContentsField.UseAzureBlobStorage)
                {
                    s.Add($"");
                    s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                    s.Add($"                .HasOne(o => o.{entity.Name}Content)");
                    s.Add($"                .WithOne(o => o.{entity.Name})");
                    s.Add($"                .HasForeignKey<{entity.Name}Content>(o => o.{entity.KeyFields.Single().Name});");
                    //if (!fileContentsField.IsNullable) s.Add($"                {entity.Name.ToCamelCase()}.Navigation(o => o.{entity.Name}Content).IsRequired();");
                    s.Add($"");
                    s.Add($"            modelBuilder.Entity<{entity.Name}Content>()");
                    s.Add($"                .ToTable(\"{entity.Name}Contents\");");
                    s.Add($"");
                }
            }

            foreach (var entity in AllEntities.OrderBy(o => o.Name))
            {
                foreach (var relationship in entity.RelationshipsAsChild.OrderBy(o => o.SortOrder).ThenBy(o => o.ParentEntity.Name))
                {
                    if (!entity.RelationshipsAsChild.Any(r => r.ParentEntityId == relationship.ParentEntityId && r.RelationshipId != relationship.RelationshipId)) continue;
                    s.Add($"            modelBuilder.Entity<{entity.Name}>()");
                    s.Add($"                .HasOne(o => o.{relationship.ParentName})");
                    s.Add($"                .WithMany(o => o.{relationship.CollectionName})");
                    if (!relationship.RelationshipFields.First().ChildField.IsNullable)
                        s.Add($"                .IsRequired()");
                    s.Add($"                .HasForeignKey(o => o.{relationship.RelationshipFields.First().ChildField.Name});");
                    s.Add($"");
                }
            }

            var smallDateTimeFields = DbContext.Fields.Where(f => f.FieldType == FieldType.SmallDateTime && f.Entity.ProjectId == CurrentEntity.ProjectId && !f.Entity.Exclude).OrderBy(f => f.Entity.Name).ThenBy(f => f.FieldOrder).ToList();
            foreach (var field in smallDateTimeFields.OrderBy(o => o.Entity.Name).ThenBy(o => o.FieldOrder))
            {
                if (field.EditPageType == EditPageType.CalculatedField) continue;
                s.Add($"            modelBuilder.Entity<{field.Entity.Name}>().Property(o => o.{field.Name}).HasColumnType(\"smalldatetime\");");
                s.Add($"");
            }

            foreach (var field in calculatedFields.OrderBy(o => o.Entity.Name).ThenBy(o => o.Name))
            {
                s.Add($"            modelBuilder.Entity<{field.Entity.Name}>()");
                s.Add($"                .Property(o => o.{field.Name})");
                s.Add($"                .HasComputedColumnSql(\"{field.CalculatedFieldDefinition}\");");
                s.Add($"");
            }

            s.Add($"        }}");

            //if (calculatedFields.Count() > 0)
            //{
            //    s.Add($"");
            //    s.Add($"        public void AddComputedColumns()");
            //    s.Add($"        {{");
            //    foreach (var field in calculatedFields)
            //    {
            //        //s.Add($"            CreateComputedColumn(\"{(field.Entity.EntityType == EntityType.User ? "AspNetUsers" : field.Entity.PluralName)}\", \"{field.Name}\", \"{field.CalculatedFieldDefinition.Replace("\"", "'")}\");");
            //    }
            //    s.Add($"        }}");
            //}
            var nullableUniques = DbContext.Fields.Where(o => o.IsUnique && o.IsNullable && o.Entity.ProjectId == CurrentEntity.ProjectId).ToList();
            s.Add($"");
            s.Add($"        public void AddNullableUniqueIndexes()");
            s.Add($"        {{");
            foreach (var field in nullableUniques.OrderBy(o => o.Entity.Name).ThenBy(o => o.Name))
            {
                s.Add($"            CreateNullableUniqueIndex(\"{field.Entity.PluralName}\", \"{field.Name}\");");
            }
            s.Add($"        }}");
            s.Add($"    }}");
            s.Add($"}}");

            return RunCodeReplacements(s.ToString(), CodeType.DbContext);
        }
    }
}