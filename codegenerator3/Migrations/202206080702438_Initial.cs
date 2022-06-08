namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.CodeReplacements",
                c => new
                    {
                        CodeReplacementId = c.Guid(nullable: false),
                        EntityId = c.Guid(nullable: false),
                        Purpose = c.String(nullable: false, maxLength: 50),
                        CodeType = c.Int(nullable: false),
                        Disabled = c.Boolean(nullable: false),
                        FindCode = c.String(nullable: false),
                        ReplacementCode = c.String(),
                        SortOrder = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.CodeReplacementId)
                .ForeignKey("dbo.Entities", t => t.EntityId)
                .Index(t => t.EntityId);
            
            CreateTable(
                "dbo.Entities",
                c => new
                    {
                        EntityId = c.Guid(nullable: false),
                        ProjectId = c.Guid(nullable: false),
                        Name = c.String(nullable: false, maxLength: 50),
                        PluralName = c.String(nullable: false, maxLength: 50),
                        FriendlyName = c.String(nullable: false, maxLength: 50),
                        PluralFriendlyName = c.String(nullable: false, maxLength: 50),
                        EntityType = c.Int(nullable: false),
                        PartialEntityClass = c.Boolean(nullable: false),
                        PartialControllerClass = c.Boolean(nullable: false),
                        Breadcrumb = c.String(maxLength: 250),
                        PreventModelDeployment = c.String(maxLength: 100),
                        PreventDTODeployment = c.String(maxLength: 100),
                        PreventDbContextDeployment = c.String(maxLength: 100),
                        PreventControllerDeployment = c.String(maxLength: 100),
                        PreventBundleConfigDeployment = c.String(maxLength: 100),
                        PreventAppRouterDeployment = c.String(maxLength: 100),
                        PreventApiResourceDeployment = c.String(maxLength: 100),
                        PreventListHtmlDeployment = c.String(maxLength: 100),
                        PreventListTypeScriptDeployment = c.String(maxLength: 100),
                        PreventEditHtmlDeployment = c.String(maxLength: 100),
                        PreventEditTypeScriptDeployment = c.String(maxLength: 100),
                        PreventAppSelectHtmlDeployment = c.String(maxLength: 100),
                        PreventAppSelectTypeScriptDeployment = c.String(maxLength: 100),
                        PreventSelectModalHtmlDeployment = c.String(maxLength: 100),
                        PreventSelectModalTypeScriptDeployment = c.String(maxLength: 100),
                        AuthorizationType = c.Int(nullable: false),
                        Exclude = c.Boolean(nullable: false),
                        ReturnOnSave = c.Boolean(nullable: false),
                        UseChildRoutes = c.Boolean(nullable: false),
                        PrimaryFieldId = c.Guid(),
                        IconClass = c.String(maxLength: 30),
                        UserFilterFieldPath = c.String(maxLength: 100),
                    })
                .PrimaryKey(t => t.EntityId)
                .ForeignKey("dbo.Projects", t => t.ProjectId)
                .ForeignKey("dbo.Fields", t => t.PrimaryFieldId)
                .Index(t => t.ProjectId)
                .Index(t => t.PrimaryFieldId);
            
            CreateTable(
                "dbo.Fields",
                c => new
                    {
                        FieldId = c.Guid(nullable: false),
                        EntityId = c.Guid(nullable: false),
                        Name = c.String(nullable: false, maxLength: 50),
                        Label = c.String(nullable: false, maxLength: 100),
                        FieldType = c.Int(nullable: false),
                        Length = c.Int(nullable: false),
                        MinLength = c.Byte(),
                        Precision = c.Byte(nullable: false),
                        Scale = c.Byte(nullable: false),
                        KeyField = c.Boolean(nullable: false),
                        IsUnique = c.Boolean(nullable: false),
                        IsUniqueOnHierarchy = c.Boolean(nullable: false),
                        IsNullable = c.Boolean(nullable: false),
                        ShowInSearchResults = c.Boolean(nullable: false),
                        SearchType = c.Int(nullable: false),
                        SortPriority = c.Int(),
                        SortDescending = c.Boolean(nullable: false),
                        FieldOrder = c.Int(nullable: false),
                        LookupId = c.Guid(),
                        EditPageType = c.Int(nullable: false),
                        ControllerInsertOverride = c.String(maxLength: 50),
                        ControllerUpdateOverride = c.String(maxLength: 50),
                        EditPageDefault = c.String(maxLength: 50),
                        CalculatedFieldDefinition = c.String(maxLength: 500),
                        RegexValidation = c.String(maxLength: 250),
                    })
                .PrimaryKey(t => t.FieldId)
                .ForeignKey("dbo.Entities", t => t.EntityId)
                .ForeignKey("dbo.Lookups", t => t.LookupId)
                .Index(t => t.EntityId)
                .Index(t => t.LookupId);
            
            CreateTable(
                "dbo.Lookups",
                c => new
                    {
                        LookupId = c.Guid(nullable: false),
                        ProjectId = c.Guid(nullable: false),
                        Name = c.String(nullable: false, maxLength: 50),
                        PluralName = c.String(nullable: false, maxLength: 50),
                        IsRoleList = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.LookupId)
                .ForeignKey("dbo.Projects", t => t.ProjectId)
                .Index(t => t.ProjectId);
            
            CreateTable(
                "dbo.LookupOptions",
                c => new
                    {
                        LookupOptionId = c.Guid(nullable: false),
                        LookupId = c.Guid(nullable: false),
                        Name = c.String(nullable: false, maxLength: 50),
                        FriendlyName = c.String(nullable: false, maxLength: 100),
                        Value = c.Int(),
                        SortOrder = c.Byte(nullable: false),
                    })
                .PrimaryKey(t => t.LookupOptionId)
                .ForeignKey("dbo.Lookups", t => t.LookupId)
                .Index(t => t.LookupId);
            
            CreateTable(
                "dbo.Projects",
                c => new
                    {
                        ProjectId = c.Guid(nullable: false),
                        Name = c.String(nullable: false, maxLength: 50),
                        WebPath = c.String(nullable: false, maxLength: 250),
                        Namespace = c.String(nullable: false, maxLength: 20),
                        AngularModuleName = c.String(nullable: false, maxLength: 20),
                        AngularDirectivePrefix = c.String(nullable: false, maxLength: 20),
                        DbContextVariable = c.String(nullable: false, maxLength: 20),
                        UserFilterFieldName = c.String(maxLength: 50),
                        ModelsPath = c.String(maxLength: 50),
                        Notes = c.String(),
                        DateCreated = c.DateTime(nullable: false, storeType: "date"),
                    })
                .PrimaryKey(t => t.ProjectId)
                .Index(t => t.Name, unique: true, name: "IX_Project_Name");
            
            CreateTable(
                "dbo.RelationshipFields",
                c => new
                    {
                        RelationshipFieldId = c.Guid(nullable: false),
                        RelationshipId = c.Guid(nullable: false),
                        ParentFieldId = c.Guid(nullable: false),
                        ChildFieldId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.RelationshipFieldId)
                .ForeignKey("dbo.Fields", t => t.ChildFieldId)
                .ForeignKey("dbo.Fields", t => t.ParentFieldId)
                .ForeignKey("dbo.Relationships", t => t.RelationshipId)
                .Index(t => t.RelationshipId)
                .Index(t => t.ParentFieldId)
                .Index(t => t.ChildFieldId);
            
            CreateTable(
                "dbo.Relationships",
                c => new
                    {
                        RelationshipId = c.Guid(nullable: false),
                        ParentEntityId = c.Guid(nullable: false),
                        ChildEntityId = c.Guid(nullable: false),
                        CollectionName = c.String(nullable: false, maxLength: 50),
                        CollectionFriendlyName = c.String(nullable: false, maxLength: 50),
                        ParentName = c.String(nullable: false, maxLength: 50),
                        ParentFriendlyName = c.String(nullable: false, maxLength: 50),
                        ParentFieldId = c.Guid(nullable: false),
                        DisplayListOnParent = c.Boolean(nullable: false),
                        Hierarchy = c.Boolean(nullable: false),
                        SortOrder = c.Int(nullable: false),
                        RelationshipAncestorLimit = c.Int(nullable: false),
                        CascadeDelete = c.Boolean(nullable: false),
                        UseSelectorDirective = c.Boolean(nullable: false),
                        UseMultiSelect = c.Boolean(nullable: false),
                        IsOneToOne = c.Boolean(nullable: false),
                        SortOrderOnChild = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.RelationshipId)
                .ForeignKey("dbo.Entities", t => t.ChildEntityId)
                .ForeignKey("dbo.Entities", t => t.ParentEntityId)
                .ForeignKey("dbo.Fields", t => t.ParentFieldId)
                .Index(t => t.ParentEntityId)
                .Index(t => t.ChildEntityId)
                .Index(t => t.ParentFieldId);
            
            CreateTable(
                "dbo.ErrorExceptions",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Message = c.String(unicode: false),
                        StackTrace = c.String(unicode: false),
                        InnerExceptionId = c.Guid(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ErrorExceptions", t => t.InnerExceptionId)
                .Index(t => t.InnerExceptionId);
            
            CreateTable(
                "dbo.Errors",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Date = c.DateTime(nullable: false),
                        Message = c.String(unicode: false),
                        Url = c.String(unicode: false),
                        Form = c.String(unicode: false),
                        UserName = c.String(maxLength: 256),
                        ExceptionId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ErrorExceptions", t => t.ExceptionId)
                .Index(t => t.ExceptionId);
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.Guid(nullable: false),
                        RoleId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.Settings",
                c => new
                    {
                        SettingsId = c.Int(nullable: false, identity: true),
                    })
                .PrimaryKey(t => t.SettingsId);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        FirstName = c.String(nullable: false, maxLength: 100),
                        LastName = c.String(nullable: false, maxLength: 100),
                        Enabled = c.Boolean(nullable: false),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Guid(nullable: false),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.Guid(nullable: false),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.Errors", "ExceptionId", "dbo.ErrorExceptions");
            DropForeignKey("dbo.ErrorExceptions", "InnerExceptionId", "dbo.ErrorExceptions");
            DropForeignKey("dbo.RelationshipFields", "RelationshipId", "dbo.Relationships");
            DropForeignKey("dbo.Relationships", "ParentFieldId", "dbo.Fields");
            DropForeignKey("dbo.Relationships", "ParentEntityId", "dbo.Entities");
            DropForeignKey("dbo.Relationships", "ChildEntityId", "dbo.Entities");
            DropForeignKey("dbo.RelationshipFields", "ParentFieldId", "dbo.Fields");
            DropForeignKey("dbo.RelationshipFields", "ChildFieldId", "dbo.Fields");
            DropForeignKey("dbo.Entities", "PrimaryFieldId", "dbo.Fields");
            DropForeignKey("dbo.Lookups", "ProjectId", "dbo.Projects");
            DropForeignKey("dbo.Entities", "ProjectId", "dbo.Projects");
            DropForeignKey("dbo.LookupOptions", "LookupId", "dbo.Lookups");
            DropForeignKey("dbo.Fields", "LookupId", "dbo.Lookups");
            DropForeignKey("dbo.Fields", "EntityId", "dbo.Entities");
            DropForeignKey("dbo.CodeReplacements", "EntityId", "dbo.Entities");
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.Errors", new[] { "ExceptionId" });
            DropIndex("dbo.ErrorExceptions", new[] { "InnerExceptionId" });
            DropIndex("dbo.Relationships", new[] { "ParentFieldId" });
            DropIndex("dbo.Relationships", new[] { "ChildEntityId" });
            DropIndex("dbo.Relationships", new[] { "ParentEntityId" });
            DropIndex("dbo.RelationshipFields", new[] { "ChildFieldId" });
            DropIndex("dbo.RelationshipFields", new[] { "ParentFieldId" });
            DropIndex("dbo.RelationshipFields", new[] { "RelationshipId" });
            DropIndex("dbo.Projects", "IX_Project_Name");
            DropIndex("dbo.LookupOptions", new[] { "LookupId" });
            DropIndex("dbo.Lookups", new[] { "ProjectId" });
            DropIndex("dbo.Fields", new[] { "LookupId" });
            DropIndex("dbo.Fields", new[] { "EntityId" });
            DropIndex("dbo.Entities", new[] { "PrimaryFieldId" });
            DropIndex("dbo.Entities", new[] { "ProjectId" });
            DropIndex("dbo.CodeReplacements", new[] { "EntityId" });
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.Settings");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.Errors");
            DropTable("dbo.ErrorExceptions");
            DropTable("dbo.Relationships");
            DropTable("dbo.RelationshipFields");
            DropTable("dbo.Projects");
            DropTable("dbo.LookupOptions");
            DropTable("dbo.Lookups");
            DropTable("dbo.Fields");
            DropTable("dbo.Entities");
            DropTable("dbo.CodeReplacements");
        }
    }
}
