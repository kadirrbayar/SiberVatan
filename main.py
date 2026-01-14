import os
import logging
import asyncio
import redis.asyncio as redis
import yaml
import pandas as pd
import io
from dotenv import load_dotenv
from telegram import Update, InlineKeyboardButton, InlineKeyboardMarkup
from telegram.ext import (
    Application,
    CommandHandler,
    MessageHandler,
    CallbackQueryHandler,
    ContextTypes,
    filters
)

load_dotenv()
BOT_TOKEN = os.getenv("BOT_TOKEN")
ADMIN_IDS = [int(x) for x in os.getenv("ADMIN_ID", "0").split(",") if x.strip().isdigit()]
REDIS_HOST = os.getenv("REDIS_HOST", "localhost")
REDIS_PORT = int(os.getenv("REDIS_PORT", 6379))
REDIS_DB = int(os.getenv("REDIS_DB", 0))
REDIS_PASSWORD = os.getenv("REDIS_PASSWORD", None)

logging.basicConfig(
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s", level=logging.WARNING
)
logger = logging.getLogger(__name__)

redis_client = redis.Redis(
    host=REDIS_HOST,
    port=REDIS_PORT,
    db=REDIS_DB,
    password=REDIS_PASSWORD,
    decode_responses=True
)

class Language:
    _strings = {}

    @classmethod
    def load(cls):
        try:
            with open("Language/tr.yml", "r", encoding="utf-8") as f:
                cls._strings = yaml.safe_load(f) or {}
            logger.info("✅ Language file loaded.")
        except Exception as e:
            logger.error(f"❌ Failed to load language file: {e}")

    @classmethod
    def get(cls, key: str) -> str:
        return cls._strings.get(key, key)

async def start_handler(update: Update, context: ContextTypes.DEFAULT_TYPE):
    args = context.args
    if args and args[0].startswith("register_"):
        try:
            group_id = args[0].split("_")[1]
            await register_start_flow(update, context, group_id)
        except IndexError:
             await update.message.reply_text("Invalid link.")
        return
    await update.message.reply_text(Language.get("start_msg"))

async def register_command(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if update.effective_user.id not in ADMIN_IDS:
        return

    chat_type = update.effective_chat.type
    if chat_type == "private":
        await update.message.reply_text(Language.get("only_group_command"))
        return
    
    group_id = update.effective_chat.id
    group_title = update.effective_chat.title

    await redis_client.sadd("groups_list", group_id)
    await redis_client.hset(f"group_info:{group_id}", "title", group_title)

    bot_username = context.bot.username
    deep_link = f"https://t.me/{bot_username}?start=register_{group_id}"
    
    keyboard = [[InlineKeyboardButton(Language.get("register_btn"), url=deep_link)]]
    reply_markup = InlineKeyboardMarkup(keyboard)
    
    await update.message.reply_text(
        text=Language.get("register_msg"),
        reply_markup=reply_markup
    )

async def register_start_flow(update: Update, context: ContextTypes.DEFAULT_TYPE, group_id_str: str):
    user = update.effective_user
    user_id = user.id
    
    existing_name = await redis_client.hget(f"group_registrations:{group_id_str}", str(user_id))
    if existing_name:
        await update.message.reply_text(Language.get("already_registered"))
        return

    try:
        member = await context.bot.get_chat_member(chat_id=group_id_str, user_id=user_id)
        if member.status in ["left", "kicked"]:
             await update.message.reply_text(Language.get("not_member"))
             return
    except Exception as e:
        logger.warning(f"Membership check failed: {e}")
        await update.message.reply_text(Language.get("not_member")) 
        return

    group_title = await redis_client.hget(f"group_info:{group_id_str}", "title") or "Unknown Group"
    prompt = Language.get("welcome_user_msg").format(group_title=group_title)
    await update.message.reply_text(prompt)
    
    context.user_data["awaiting_name"] = True
    context.user_data["reg_group_id"] = group_id_str

async def message_handler(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if update.effective_chat.type != "private":
        return

    if context.user_data.get("awaiting_name"):
        name_surname = update.message.text
        if not name_surname:
            await update.message.reply_text(Language.get("invalid_format"))
            return
            
        user = update.effective_user
        user_id = user.id
        group_id = context.user_data.get("reg_group_id")
        
        if await redis_client.hget(f"group_registrations:{group_id}", str(user_id)):
             await update.message.reply_text(Language.get("already_registered"))
             context.user_data["awaiting_name"] = False
             return

        await redis_client.hset(f"group_registrations:{group_id}", str(user_id), name_surname)
        await redis_client.hset(
            f"user_info:{user_id}",
            mapping={
                "user_id": user_id,
                "first_name": user.first_name,
                "last_name": user.last_name or "",
                "username": user.username or ""
            }
        )
        
        context.user_data["awaiting_name"] = False
        await update.message.reply_text(Language.get("register_success"))

async def users_command(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if update.effective_user.id not in ADMIN_IDS:
        return

    groups = await redis_client.smembers("groups_list")
    if not groups:
        await update.message.reply_text(Language.get("no_groups"))
        return

    page = 0
    await send_group_list(update, context, list(groups), page)

async def send_group_list(update, context, groups, page):
    per_page = 4
    start = page * per_page
    end = start + per_page
    current_groups = groups[start:end]
    
    keyboard = []
    for gid in current_groups:
        title = await redis_client.hget(f"group_info:{gid}", "title") or f"Group {gid}"
        keyboard.append([InlineKeyboardButton(title, callback_data=f"grp_sel|{gid}")])
    
    nav_row = []
    if page > 0:
        nav_row.append(InlineKeyboardButton("⬅️", callback_data=f"grp_page|{page-1}"))
    if end < len(groups):
        nav_row.append(InlineKeyboardButton("➡️", callback_data=f"grp_page|{page+1}"))
    
    if nav_row:
        keyboard.append(nav_row)
     
    reply_markup = InlineKeyboardMarkup(keyboard) 
    text = Language.get("select_group")
    
    if update.callback_query:
        await update.callback_query.edit_message_text(text, reply_markup=reply_markup)
    else:
        await update.message.reply_text(text, reply_markup=reply_markup)

async def callback_handler(update: Update, context: ContextTypes.DEFAULT_TYPE):
    query = update.callback_query
    await query.answer()
    
    data = query.data
    if data.startswith("grp_page|"):
        page = int(data.split("|")[1])
        groups = await redis_client.smembers("groups_list")
        await send_group_list(update, context, list(groups), page)
        
    elif data.startswith("grp_sel|"):
        group_id = data.split("|")[1]
        await generate_report(update, context, group_id)

async def generate_report(update, context, group_id):
    group_title = await redis_client.hget(f"group_info:{group_id}", "title") or Language.get("unknown_group")
    
    try:
        member_count = await context.bot.get_chat_member_count(group_id)
    except:
        member_count = 0

    registrations = await redis_client.hgetall(f"group_registrations:{group_id}")
    report_data = []
    registered_count = len(registrations)
    
    for uid, real_name in registrations.items():
        user_data = await redis_client.hgetall(f"user_info:{uid}")
        
        try:
            member = await context.bot.get_chat_member(group_id, int(uid))
            if member.status in ["left", "kicked"]:
               pass
        except:
             pass

        report_data.append({
            "User ID": uid,
            "Telegram Name": f"{user_data.get('first_name', 'Unknown')} {user_data.get('last_name', '')}",
            "Username": user_data.get('username', 'None'),
            "Real Name": real_name,
            "Group ID": group_id
        })
    
    unregistered_count = max(0, member_count - registered_count)  
    if not report_data:
        csv_file = io.StringIO(Language.get("no_users_csv"))
    else:
        df = pd.DataFrame(report_data)
        csv_file = io.StringIO()
        df.to_csv(csv_file, index=False)
    
    csv_file.seek(0) 
    caption = Language.get("csv_caption").format(
        group_title=group_title,
        member_count=member_count,
        registered_count=registered_count,
        unregistered_count=unregistered_count
    )
    
    await context.bot.send_document(
        chat_id=update.effective_chat.id,
        document=io.BytesIO(csv_file.getvalue().encode()),
        filename=f"{group_title}_Users.csv",
        caption=caption
    )

async def info_command(update: Update, context: ContextTypes.DEFAULT_TYPE):
    if update.effective_user.id not in ADMIN_IDS:
        return

    args = context.args
    target_id = None
    
    if update.message.reply_to_message:
        target_id = update.message.reply_to_message.from_user.id
    elif args:
        target_id = args[0]
        
    if not target_id:
        await update.message.reply_text(Language.get("input_user_id"))
        return
        
    user_data = await redis_client.hgetall(f"user_info:{target_id}")
    if not user_data:
        await update.message.reply_text(Language.get("info_not_found"))
        return
        
    groups = await redis_client.smembers("groups_list")
    registrations_text = ""
    
    for gid in groups:
        real_name = await redis_client.hget(f"group_registrations:{gid}", str(target_id))
        if real_name:
            g_title = await redis_client.hget(f"group_info:{gid}", "title") or gid
            registrations_text += f"- {g_title}: {real_name}\n"
            
    info_text = Language.get("info_template").format(
        user_id=user_data.get('user_id', target_id),
        first_name=user_data.get('first_name', '?'),
        last_name=user_data.get('last_name', ''),
        username=user_data.get('username', ''),
        real_name=registrations_text or "No registrations"
    )
    
    await update.message.reply_text(info_text)

def main():
    Language.load()
    
    if not BOT_TOKEN:
        logger.error("❌ BOT_TOKEN not found!")
        return

    application = Application.builder().token(BOT_TOKEN).build()
    application.add_handler(CommandHandler("start", start_handler))
    application.add_handler(CommandHandler("register", register_command))
    application.add_handler(CommandHandler("users", users_command))
    application.add_handler(CommandHandler("info", info_command))  
    application.add_handler(CallbackQueryHandler(callback_handler))
    application.add_handler(MessageHandler(filters.TEXT & ~filters.COMMAND, message_handler))
    
    logger.info("🚀 SiberVatanBot started...")
    application.run_polling()

if __name__ == "__main__":
    main()
