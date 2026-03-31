namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveNamespace : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.Projects", "Namespace");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Projects", "Namespace", c => c.String(nullable: false, maxLength: 20));
        }
    }
}
