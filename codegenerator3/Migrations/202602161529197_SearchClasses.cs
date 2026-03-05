namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class SearchClasses : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Fields", "SearchClasses", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Fields", "SearchClasses");
        }
    }
}
