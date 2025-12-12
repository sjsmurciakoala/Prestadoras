USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[PriceIdx]    Script Date: 10/12/2025 14:05:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[PriceIdx](
	[siYear] [smallint] NOT NULL,
	[siMonth] [tinyint] NOT NULL,
	[Factor] [decimal](28, 9) NOT NULL,
 CONSTRAINT [PriceIdx0] PRIMARY KEY CLUSTERED 
(
	[siYear] ASC,
	[siMonth] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[PriceIdx] ADD  DEFAULT ((0)) FOR [siYear]
GO

ALTER TABLE [dbo].[PriceIdx] ADD  DEFAULT ((0)) FOR [siMonth]
GO

ALTER TABLE [dbo].[PriceIdx] ADD  DEFAULT ((0)) FOR [Factor]
GO

ALTER TABLE [dbo].[PriceIdx]  WITH CHECK ADD CHECK  (([siMonth]>=(1) AND [siMonth]<=(12)))
GO


