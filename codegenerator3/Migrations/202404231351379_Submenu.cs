namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class Submenu : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Entities", "Submenu", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Entities", "Submenu");
        }
    }
}
