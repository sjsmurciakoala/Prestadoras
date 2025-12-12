USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01RpFields]    Script Date: 10/12/2025 13:44:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01RpFields](
	[folderid] [int] NOT NULL,
	[itemtype] [int] NOT NULL,
	[itemname] [varchar](60) NOT NULL,
	[modified] [datetime] NOT NULL,
	[itemid] [int] IDENTITY(1,1) NOT NULL,
	[itemsize] [int] NULL,
	[deleted] [datetime] NULL,
	[template] [image] NULL,
PRIMARY KEY CLUSTERED 
(
	[folderid] ASC,
	[itemtype] ASC,
	[itemname] ASC,
	[modified] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


