namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Sortable : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Fields", "Sortable", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Fields", "Sortable");
        }
    }
}
