namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class displayPipe : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Fields", "DisplayPipe", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Fields", "DisplayPipe");
        }
    }
}
