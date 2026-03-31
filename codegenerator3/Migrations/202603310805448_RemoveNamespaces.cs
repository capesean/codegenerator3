namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveNamespaces : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Projects", "ModelsNamespace");
            DropColumn("dbo.Projects", "WebNamespace");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Projects", "WebNamespace", c => c.String(nullable: false, maxLength: 20));
            AddColumn("dbo.Projects", "ModelsNamespace", c => c.String(nullable: false, maxLength: 20));
        }
    }
}
