namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Menu : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Entities", "Menu", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Entities", "Menu");
        }
    }
}
