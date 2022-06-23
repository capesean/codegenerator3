namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class test : DbMigration
    {
        public override void Up()
        {
            RenameColumn("dbo.Entities", "PreventBundleConfigDeployment", "PreventGeneratedModuleDeployment");
            AddColumn("dbo.Entities", "PreventSharedModuleDeployment", c => c.String(maxLength: 100));
        }
        
        public override void Down()
        {
            RenameColumn("dbo.Entities", "PreventGeneratedModuleDeployment", "PreventBundleConfigDeployment");
            DropColumn("dbo.Entities", "PreventSharedModuleDeployment");
        }
    }
}
