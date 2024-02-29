namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class displayClasses : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Fields", "DisplayClasses", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Fields", "DisplayClasses");
        }
    }
}
