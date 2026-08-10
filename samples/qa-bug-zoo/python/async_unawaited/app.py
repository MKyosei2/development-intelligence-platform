import asyncio

async def unfinished():
    await asyncio.sleep(0)

if __name__ == "__main__":
    unfinished()
