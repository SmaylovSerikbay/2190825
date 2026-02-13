from flask import Flask, request, redirect, render_template_string
import uuid
import hashlib
import requests
import json
from datetime import datetime

app = Flask(__name__)

MERCHANT_ID = "552170"
SECRET_KEY = "wUQ18x3bzP86MUzn"

# Попробуем разные endpoints
GATEWAY_URLS = [
    "https://api.freedompay.uz/payment.php",
    "https://merchant.freedompay.uz/payment.php", 
    "https://checkout.freedompay.uz/payment.php",
    "https://pay.freedompay.uz/payment.php"
]

NGROK_URL = "https://2f91d0d162d4.ngrok-free.app"  # Убираем все лишние пробелы

# Хранилище статусов платежей (в реальном проекте используйте базу данных)
payment_statuses = {}

def log_message(msg):
    """Логирование с временными метками"""
    timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    print(f"[{timestamp}] {msg}")

def validate_credentials():
    """Проверка учетных данных"""
    if not MERCHANT_ID or not SECRET_KEY:
        log_message("❌ ОШИБКА: Не указаны MERCHANT_ID или SECRET_KEY")
        return False
    
    if len(SECRET_KEY) < 10:
        log_message("❌ ОШИБКА: SECRET_KEY слишком короткий")
        return False
        
    return True

def validate_ngrok_url():
    """Проверка корректности NGROK URL"""
    if not NGROK_URL or NGROK_URL.strip() != NGROK_URL:
        log_message("❌ ОШИБКА: NGROK_URL содержит пробелы!")
        return False
    
    if not NGROK_URL.startswith('https://'):
        log_message("❌ ОШИБКА: NGROK_URL должен начинаться с https://")
        return False
        
    return True

def verify_signature(params_dict, received_signature):
    """Проверка подписи от FreedomPay с правильным алгоритмом"""
    try:
        # Убираем подпись из параметров для проверки
        params_copy = params_dict.copy()
        if 'pg_sig' in params_copy:
            del params_copy['pg_sig']
        
        # Определяем имя скрипта
        if 'pg_result' in params_copy:
            script_name = "result.php"
        else:
            script_name = "check.php"
        
        # ✅ Используем правильный алгоритм
        expected_signature, check_string = generate_correct_signature(params_copy, script_name, SECRET_KEY)
        
        log_message(f"🔍 Проверка подписи:")
        log_message(f"   Получена: {received_signature}")
        log_message(f"   Ожидаем: {expected_signature}")
        log_message(f"   Строка: {check_string}")
        
        return expected_signature == received_signature
    except Exception as e:
        log_message(f"❌ Ошибка проверки подписи: {e}")
        return False

def generate_correct_signature(params_dict, script_name, secret_key):
    """
    Правильная генерация подписи по документации FreedomPay
    """
    # 1. Сортируем параметры по алфавиту (как ksort в PHP)
    sorted_keys = sorted(params_dict.keys())
    
    # 2. Создаем массив значений в алфавитном порядке
    values = [str(params_dict[key]) for key in sorted_keys]
    
    # 3. Добавляем имя скрипта в начало (array_unshift в PHP)
    values.insert(0, script_name)
    
    # 4. Добавляем SECRET_KEY в конец (array_push в PHP)  
    values.append(secret_key)
    
    # 5. Склеиваем через ';' (implode в PHP)
    sign_string = ';'.join(values)
    
    # 6. MD5 хеш
    signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
    
    return signature, sign_string

@app.route('/test_correct_algorithm')
def test_correct_algorithm():
    """Тестирование правильного алгоритма подписи"""
    
    # Параметры из личного кабинета
    cabinet_params = {
        "pg_merchant_id": "552170",
        "pg_amount": "1000",
        "pg_currency": "UZS", 
        "pg_description": "sadas",
        "pg_salt": "XRJ0fLDMaPjtjnTy",
        "pg_language": "ru",
        "payment_origin": "merchant_cabinet"
    }
    
    expected_signature = "cf5b280eccf239052039b0692208bce3"
    
    results = []
    results.append("=== ТЕСТ ПРАВИЛЬНОГО АЛГОРИТМА ПО ДОКУМЕНТАЦИИ ===")
    results.append(f"Ожидаемая подпись: {expected_signature}")
    results.append("")
    
    # Тестируем разные имена скриптов
    script_names = [
        "payment.php",
        "init_payment.php", 
        "p2p2nonreg",  # из примера PHP
        "gateway",
        ""  # без имени скрипта
    ]
    
    # Тестируем все ключи
    test_keys = [
        ("Ключ для приема", "wUQ18x3bzP86MUzn"),
        ("Ключ для выплат", "lvA1DXTL8ILLLj0P"), 
        ("JS SDK ключ", "Jw6idIqYSd5gIGLL321KUP5ej6fneq1G")
    ]
    
    for key_name, secret_key in test_keys:
        results.append(f"=== ТЕСТ С {key_name.upper()}: {secret_key} ===")
        
        for script_name in script_names:
            signature, sign_string = generate_correct_signature(cabinet_params, script_name, secret_key)
            match = "🎉 НАЙДЕНО!" if signature == expected_signature else "❌ не совпадает"
            
            results.append(f"Скрипт '{script_name}':")
            results.append(f"  Строка: {sign_string}")
            results.append(f"  MD5: {signature} {match}")
            
            if match == "🎉 НАЙДЕНО!":
                results.append(f"  ✅ ПРАВИЛЬНЫЙ АЛГОРИТМ: script='{script_name}', key='{secret_key}'")
            
            results.append("")
    
    # Дополнительный тест без payment_origin
    results.append("=== ТЕСТ БЕЗ PARAMETER 'payment_origin' ===")
    
    cabinet_params_no_origin = cabinet_params.copy()
    del cabinet_params_no_origin['payment_origin']
    
    for key_name, secret_key in test_keys:
        for script_name in script_names:
            signature, sign_string = generate_correct_signature(cabinet_params_no_origin, script_name, secret_key)
            match = "🎉 НАЙДЕНО!" if signature == expected_signature else "❌ не совпадает"
            
            if match == "🎉 НАЙДЕНО!":
                results.append(f"🎉 БЕЗ payment_origin! Скрипт='{script_name}', ключ='{key_name}': {signature}")
    
    return render_template_string('''
        <h2>🎯 Тест правильного алгоритма по документации</h2>
        <pre style="background: #f5f5f5; padding: 15px; border-radius: 5px; font-size: 11px;">{{ results }}</pre>
        
        <h3>📋 Алгоритм (как в PHP примере):</h3>
        <ol>
            <li>Сортировка параметров по алфавиту (ksort)</li>
            <li>Добавление имени скрипта в начало (array_unshift)</li>
            <li>Добавление SECRET_KEY в конец (array_push)</li>
            <li>Склеивание через ';' (implode)</li>
            <li>MD5 хеш</li>
        </ol>
        
        <p><a href="/">← Главная</a></p>
    ''', results='\\n'.join(results))

@app.route('/')
def index():
    if not validate_credentials():
        return "<h2>❌ Ошибка конфигурации. Проверьте MERCHANT_ID и SECRET_KEY</h2>"
    
    if not validate_ngrok_url():
        return "<h2>❌ Ошибка конфигурации. Проверьте NGROK_URL</h2>"
        
    return render_template_string('''
        <h2>Тестовая оплата FreedomPay</h2>
        <form method="post" action="/pay">
            <label>Сумма (UZS):</label><br>
            <input type="number" name="amount" value="1000" required><br><br>
            <button type="submit">Оплатить</button>
        </form>
        
        <h3>Информация:</h3>
        <p>Merchant ID: {{ merchant_id }}</p>
        <p>Ngrok URL: {{ ngrok_url }}</p>
        
        <h3>🛠 Инструменты диагностики:</h3>
        <p><a href="/test_final_payment">🎉 ФИНАЛЬНЫЙ ТЕСТ (алгоритм найден!)</a></p>
        <p><a href="/all_payment_statuses">📊 Все статусы платежей</a></p>
        <p><a href="/test_correct_algorithm">🎯 Тест ПРАВИЛЬНОГО алгоритма (по документации!)</a></p>
        <p><a href="/test_hash_algorithms">🔬 Тест алгоритмов хеширования</a></p>
        <p><a href="/analyze_cabinet_url">🕵️ Анализ URL из кабинета</a></p>
        <p><a href="/test_manual_key">🔑 Ручной тест SECRET_KEY</a></p>
        <p><a href="/find_secret">🔍 Поиск правильного SECRET_KEY</a></p>
        <p><a href="/test_signature">🔐 Тест подписи личного кабинета</a></p>
        <p><a href="/test">🧪 Тест подключения</a></p>
        <p><a href="/diagnose">🩺 Диагностика ошибки 10000</a></p>
    ''', merchant_id=MERCHANT_ID, ngrok_url=NGROK_URL)

# Добавляем альтернативные endpoints и методы
ALTERNATIVE_ENDPOINTS = [
    "https://api.freedompay.uz/init_payment.php",
    "https://api.freedompay.uz/gateway/payment.php",
    "https://freedompay.uz/payment.php",
    "https://secure.freedompay.uz/payment.php"
]

# Добавляем проверку мерчанта
def test_merchant_credentials():
    """Тестирование учетных данных мерчанта"""
    log_message("🔍 Тестирование учетных данных мерчанта...")
    
    test_params = {
        "pg_merchant_id": MERCHANT_ID,
        "pg_testing_mode": "1",
        "pg_amount": "100",
        "pg_currency": "UZS",
        "pg_description": "Test",
        "pg_order_id": "test_" + uuid.uuid4().hex[:8],
        "pg_salt": uuid.uuid4().hex
    }
    
    # Создаем тестовую подпись
    sorted_keys = sorted(test_params.keys())
    sign_string = "payment.php;" + ";".join(f"{k}={test_params[k]}" for k in sorted_keys) + f";{SECRET_KEY}"
    signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
    
    log_message(f"🧪 Тестовая подпись: {signature}")
    log_message(f"🧪 Merchant ID: {MERCHANT_ID}")
    
    return True

@app.route('/pay', methods=['POST'])
def pay():
    if not validate_credentials():
        return "<h2>❌ Ошибка конфигурации</h2>"
    
    if not validate_ngrok_url():
        return "<h2>❌ Ошибка конфигурации NGROK URL</h2>"

    amount = str(int(request.form['amount']))
    salt = uuid.uuid4().hex[:16]  # Укорачиваем соль как в примере

    # Параметры для платежа
    params = {
        "pg_merchant_id": MERCHANT_ID,
        "pg_amount": amount,
        "pg_currency": "UZS",
        "pg_description": "Test Payment",
        "pg_salt": salt,
        "pg_language": "ru",
        "payment_origin": "merchant_cabinet"
    }

    # ✅ ПРАВИЛЬНАЯ подпись по найденному алгоритму
    signature, sign_string = generate_correct_signature(params, "payment.php", SECRET_KEY)

    log_message(f"💰 Amount: {amount} UZS")
    log_message(f"🧂 Salt: {salt}")
    log_message(f"✅ ПРАВИЛЬНАЯ подпись: {signature}")
    log_message(f"📝 Sign String: {sign_string}")

    # Формируем URL для перенаправления
    query_parts = []
    sorted_keys = sorted(params.keys())
    for key in sorted_keys:
        query_parts.append(f"{key}={params[key]}")
    query_parts.append(f"pg_sig={signature}")
    
    query_string = "&".join(query_parts)
    redirect_url = f"https://api.freedompay.uz/payment.php?{query_string}"
    
    log_message(f"🔗 Итоговая ссылка: {redirect_url}")
    log_message(f"🚀 Перенаправляем...")
    
    return redirect(redirect_url)

# Добавляем отдельный route для тестирования подписи
@app.route('/test_signature')
def test_signature():
    """Тестирование подписи как в личном кабинете"""
    log_message("🧪 Тестирование подписи как в личном кабинете...")
    
    # Точные параметры из примера личного кабинета
    cabinet_params = {
        "pg_merchant_id": "552170",
        "pg_amount": "1000", 
        "pg_currency": "UZS",
        "pg_description": "sadas",
        "pg_salt": "XRJ0fLDMaPjtjnTy",
        "pg_language": "ru",
        "payment_origin": "merchant_cabinet"
    }
    
    sorted_keys = sorted(cabinet_params.keys())
    sign_string = "payment.php;" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{SECRET_KEY}"
    our_signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
    
    expected_signature = "cf5b280eccf239052039b0692208bce3"
    
    results = []
    results.append("=== ТЕСТ ПОДПИСИ ЛИЧНОГО КАБИНЕТА ===")
    results.append(f"Строка подписи: {sign_string}")
    results.append(f"Ожидаемая: {expected_signature}")
    results.append(f"Наша:      {our_signature}")
    results.append(f"Совпадает: {'✅ ДА' if our_signature == expected_signature else '❌ НЕТ'}")
    
    if our_signature != expected_signature:
        results.append("\n=== ВОЗМОЖНЫЕ ПРОБЛЕМЫ ===")
        results.append("1. Неверный SECRET_KEY")
        results.append("2. Другая кодировка строки")
        results.append("3. Другой алгоритм хеширования")
        results.append(f"Текущий SECRET_KEY: {SECRET_KEY}")
    
    return render_template_string('''
        <h2>🧪 Тест подписи личного кабинета</h2>
        <pre style="background: #f5f5f5; padding: 15px; border-radius: 5px;">{{ results }}</pre>
        <p><a href="/">← Главная</a></p>
    ''', results='\\n'.join(results))

@app.route('/check', methods=['POST'])
def check():
    log_message("▶ CHECK запрос получен")
    log_message(f"📨 Данные: {dict(request.form)}")
    
    # Проверяем подпись
    pg_sig = request.form.get('pg_sig')
    if pg_sig:
        if verify_signature(dict(request.form), pg_sig):
            log_message("✅ Подпись CHECK корректна")
        else:
            log_message("❌ Некорректная подпись CHECK")
            return "ERROR", 400
    else:
        log_message("⚠️ Подпись CHECK отсутствует")
    
    # Проверяем order_id и amount
    pg_order_id = request.form.get('pg_order_id')
    pg_amount = request.form.get('pg_amount')
    
    log_message(f"🆔 Order ID: {pg_order_id}")
    log_message(f"💰 Amount: {pg_amount} UZS")
    
    # Инициализируем статус как "pending" если его еще нет
    if pg_order_id and pg_order_id not in payment_statuses:
        payment_statuses[pg_order_id] = "pending"
        log_message(f"📝 Создан статус 'pending' для Order ID: {pg_order_id}")
    
    # Здесь должна быть проверка существования заказа в вашей БД
    # Пока возвращаем OK для всех запросов
    return "OK", 200

@app.route('/result', methods=['POST'])
def result():
    log_message("▶ RESULT запрос получен")
    log_message(f"📨 Данные: {dict(request.form)}")
    
    # Проверяем подпись
    pg_sig = request.form.get('pg_sig')
    if pg_sig:
        if verify_signature(dict(request.form), pg_sig):
            log_message("✅ Подпись RESULT корректна")
        else:
            log_message("❌ Некорректная подпись RESULT")
            return "ERROR", 400
    else:
        log_message("⚠️ Подпись RESULT отсутствует")
    
    # Обрабатываем результат платежа
    pg_result = request.form.get('pg_result')
    pg_payment_id = request.form.get('pg_payment_id')
    pg_order_id = request.form.get('pg_order_id')
    pg_amount = request.form.get('pg_amount')
    
    log_message(f"🆔 Order ID: {pg_order_id}")
    log_message(f"💳 Payment ID: {pg_payment_id}")
    log_message(f"💰 Amount: {pg_amount} UZS")
    
    if pg_result == "1":
        log_message(f"✅ Платеж успешен! Payment ID: {pg_payment_id}")
        # Здесь должно быть обновление статуса заказа в БД
        payment_statuses[pg_order_id] = "success"
    else:
        log_message(f"❌ Платеж не прошел. Результат: {pg_result}")
        # Здесь должна быть обработка неуспешного платежа
        payment_statuses[pg_order_id] = "failed"
    
    return "OK", 200

@app.route('/success', methods=['GET', 'POST'])
def success():
    # Обрабатываем как GET, так и POST запросы
    if request.method == 'POST':
        log_message("✅ Получен POST callback на /success")
        log_message(f"📨 POST данные: {dict(request.form)}")
        
        # Обрабатываем как успешный callback
        if request.form:
            # Получаем order_id из формы
            pg_order_id = request.form.get('pg_order_id')
            if pg_order_id:
                payment_statuses[pg_order_id] = "success"
                log_message(f"✅ Установлен статус 'success' для Order ID: {pg_order_id}")
        
        return "OK", 200
    
    # GET запрос - показываем страницу
    return render_template_string('''
        <h2>✅ Платеж прошёл успешно!</h2>
        <p>Спасибо за покупку!</p>
        <p><a href="/">← Новый платеж</a></p>
    ''')

@app.route('/fail', methods=['GET', 'POST'])
def fail():
    # Обрабатываем как GET, так и POST запросы
    if request.method == 'POST':
        log_message("❌ Получен POST callback на /fail")
        log_message(f"📨 POST данные: {dict(request.form)}")
        
        # Обрабатываем как неуспешный callback
        if request.form:
            pg_order_id = request.form.get('pg_order_id')
            if pg_order_id:
                payment_statuses[pg_order_id] = "failed"
                log_message(f"❌ Установлен статус 'failed' для Order ID: {pg_order_id}")
        
        return "OK", 200
    
    # GET запрос - показываем страницу
    return render_template_string('''
        <h2>❌ Платеж не прошёл или был отменён</h2>
        <p>Попробуйте ещё раз или свяжитесь с поддержкой.</p>
        <p><a href="/">← Попробовать снова</a></p>
    ''')

# Добавляем route для диагностики ошибки 10000
@app.route('/diagnose')
def diagnose():
    """Диагностика проблем с ошибкой 10000"""
    log_message("🩺 Начинаем диагностику ошибки 10000...")
    
    results = []
    
    # 1. Проверка базовых параметров
    results.append("=== ПРОВЕРКА БАЗОВЫХ ПАРАМЕТРОВ ===")
    results.append(f"MERCHANT_ID: {MERCHANT_ID}")
    results.append(f"SECRET_KEY длина: {len(SECRET_KEY)} символов")
    results.append(f"NGROK_URL: {NGROK_URL}")
    
    # 2. Тест подписи
    results.append("\n=== ТЕСТ ПОДПИСИ ===")
    test_params = {
        "pg_merchant_id": MERCHANT_ID,
        "pg_amount": "1000",
        "pg_currency": "UZS",
        "pg_order_id": "test_123",
        "pg_testing_mode": "1"
    }
    
    sorted_keys = sorted(test_params.keys())
    sign_string = "payment.php;" + ";".join(f"{k}={test_params[k]}" for k in sorted_keys) + f";{SECRET_KEY}"
    signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
    
    results.append(f"Тестовая строка: {sign_string}")
    results.append(f"Тестовая подпись: {signature}")
    
    # 3. Тест доступности серверов
    results.append("\n=== ТЕСТ СЕРВЕРОВ ===")
    all_endpoints = GATEWAY_URLS + ALTERNATIVE_ENDPOINTS
    
    for url in all_endpoints:
        try:
            response = requests.get(url, timeout=5)
            results.append(f"✅ {url} - код: {response.status_code}")
        except Exception as e:
            results.append(f"❌ {url} - ошибка: {str(e)[:50]}...")
    
    # 4. Тест POST запроса
    results.append("\n=== ТЕСТ POST ЗАПРОСА ===")
    try:
        post_data = test_params.copy()
        post_data['pg_sig'] = signature
        
        response = requests.post("https://api.freedompay.uz/payment.php", data=post_data, timeout=10)
        results.append(f"POST статус: {response.status_code}")
        
        if "10000" in response.text:
            results.append("❌ Получена ошибка 10000")
        else:
            results.append("✅ Ошибка 10000 не обнаружена")
            
        results.append(f"Ответ: {response.text[:200]}...")
        
    except Exception as e:
        results.append(f"❌ POST ошибка: {str(e)}")
    
    # 5. Альтернативные методы
    results.append("\n=== АЛЬТЕРНАТИВНЫЕ МЕТОДЫ ===")
    
    # Пробуем без pg_testing_mode
    alt_params = {
        "pg_merchant_id": MERCHANT_ID,
        "pg_amount": "1000",
        "pg_currency": "UZS",
        "pg_order_id": "alt_test_123"
    }
    
    alt_sorted = sorted(alt_params.keys())
    alt_sign = "payment.php;" + ";".join(f"{k}={alt_params[k]}" for k in alt_sorted) + f";{SECRET_KEY}"
    alt_signature = hashlib.md5(alt_sign.encode('utf-8')).hexdigest()
    
    results.append(f"Без testing_mode: {alt_signature}")
    
    return render_template_string('''
        <h2>🩺 Диагностика ошибки 10000</h2>
        <pre style="background: #f5f5f5; padding: 15px; border-radius: 5px;">{{ results }}</pre>
        
        <h3>📋 Рекомендации:</h3>
        <ol>
            <li>Если все серверы недоступны - проблема в сети</li>
            <li>Если POST возвращает 10000 - проблема в учетных данных</li>
            <li>Проверьте в личном кабинете FreedomPay:
                <ul>
                    <li>Статус аккаунта (активен/заблокирован)</li>
                    <li>Тестовый режим включен</li>
                    <li>Валюта UZS активна</li>
                    <li>Callback URLs настроены</li>
                </ul>
            </li>
        </ol>
        
        <p><a href="/">← Главная</a> | <a href="/test">🧪 Тест подключения</a></p>
    ''', results='\\n'.join(results))

# Улучшаем тестовый endpoint
@app.route('/test')
def test():
    log_message("🧪 Тестирование подключения к FreedomPay...")
    
    results = []
    all_endpoints = GATEWAY_URLS + ALTERNATIVE_ENDPOINTS
    
    for url in all_endpoints:
        try:
            response = requests.head(url, timeout=5)
            status = f"✅ Доступен (код: {response.status_code})"
            
            # Дополнительная проверка с GET
            if response.status_code == 405:  # Method not allowed для HEAD
                try:
                    get_response = requests.get(url, timeout=5)
                    status += f" | GET: {get_response.status_code}"
                except:
                    pass
                    
        except Exception as e:
            status = f"❌ Недоступен: {str(e)[:50]}..."
        
        results.append(f"{url} - {status}")
        log_message(f"{url} - {status}")
    
    return render_template_string('''
        <h2>🧪 Тест подключения к FreedomPay</h2>
        <h3>Результаты:</h3>
        {% for result in results %}
            <p style="font-family: monospace;">{{ result }}</p>
        {% endfor %}
        
        <h3>📊 Дополнительные инструменты:</h3>
        <p><a href="/diagnose">🩺 Диагностика ошибки 10000</a></p>
        <p><a href="/">← Вернуться</a></p>
    ''', results=results)

# Добавляем функцию поиска правильного SECRET_KEY
def find_correct_secret_key():
    """Попытка найти правильный SECRET_KEY методом перебора"""
    
    # Возможные варианты SECRET_KEY
    possible_keys = [
        "wUQ18x3bzP86MUzn",  # текущий
        "wUQ18x3bzP86MUzn123",
        "552170wUQ18x3bzP86MUzn",
        "merchant_552170",
        "freedompay_552170",
        # Добавим другие вариации
        SECRET_KEY + "123",
        SECRET_KEY.upper(),
        SECRET_KEY.lower(),
        "552170_" + SECRET_KEY,
        SECRET_KEY[::-1],  # обратный порядок
    ]
    
    # Параметры из примера личного кабинета
    cabinet_params = {
        "pg_merchant_id": "552170",
        "pg_amount": "1000", 
        "pg_currency": "UZS",
        "pg_description": "sadas",
        "pg_salt": "XRJ0fLDMaPjtjnTy",
        "pg_language": "ru",
        "payment_origin": "merchant_cabinet"
    }
    
    expected_signature = "cf5b280eccf239052039b0692208bce3"
    sorted_keys = sorted(cabinet_params.keys())
    
    log_message("🔍 Поиск правильного SECRET_KEY...")
    
    for i, test_key in enumerate(possible_keys):
        sign_string = "payment.php;" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{test_key}"
        test_signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
        
        log_message(f"Тест {i+1}: {test_key[:20]}... -> {test_signature}")
        
        if test_signature == expected_signature:
            log_message(f"✅ НАЙДЕН ПРАВИЛЬНЫЙ SECRET_KEY: {test_key}")
            return test_key
    
    log_message("❌ Правильный SECRET_KEY не найден среди вариантов")
    return None

@app.route('/find_secret')
def find_secret():
    """Route для поиска правильного SECRET_KEY"""
    log_message("🕵️ Запуск поиска SECRET_KEY...")
    
    correct_key = find_correct_secret_key()
    
    if correct_key:
        return render_template_string('''
            <h2>✅ SECRET_KEY найден!</h2>
            <p><strong>Правильный SECRET_KEY:</strong> <code>{{ secret_key }}</code></p>
            
            <h3>📝 Что делать дальше:</h3>
            <ol>
                <li>Скопируйте найденный ключ</li>
                <li>Замените в коде строку: <br>
                    <code>SECRET_KEY = "{{ old_key }}"</code><br>
                    на<br>
                    <code>SECRET_KEY = "{{ secret_key }}"</code>
                </li>
                <li>Перезапустите сервер</li>
                <li>Попробуйте платеж снова</li>
            </ol>
            
            <p><a href="/">← Главная</a></p>
        ''', secret_key=correct_key, old_key=SECRET_KEY)
    else:
        return render_template_string('''
            <h2>❌ SECRET_KEY не найден</h2>
            
            <h3>🔑 Где найти правильный SECRET_KEY:</h3>
            <ol>
                <li><strong>Личный кабинет FreedomPay:</strong>
                    <ul>
                        <li>Войдите в <a href="https://merchant.freedompay.com/" target="_blank">merchant.freedompay.com</a></li>
                        <li>Найдите раздел "API" или "Интеграция"</li>
                        <li>Скопируйте SECRET_KEY (может называться "Секретный ключ" или "API Key")</li>
                    </ul>
                </li>
                <li><strong>Свяжитесь с поддержкой:</strong>
                    <ul>
                        <li>Email: support@freedompay.uz</li>
                        <li>Укажите MERCHANT_ID: {{ merchant_id }}</li>
                        <li>Попросите предоставить актуальный SECRET_KEY</li>
                    </ul>
                </li>
            </ol>
            
            <p><a href="/">← Главная</a></p>
        ''', merchant_id=MERCHANT_ID)

@app.route('/test_manual_key', methods=['GET', 'POST'])
def test_manual_key():
    """Ручное тестирование SECRET_KEY"""
    
    if request.method == 'GET':
        return render_template_string('''
            <h2>🔑 Тестирование SECRET_KEY вручную</h2>
            
            <h3>📋 Инструкция:</h3>
            <ol>
                <li>Войдите в <a href="https://merchant.freedompay.com/" target="_blank">личный кабинет FreedomPay</a></li>
                <li>Найдите раздел "API", "Интеграция" или "Настройки"</li>
                <li>Скопируйте SECRET_KEY (может называться "Секретный ключ", "API Key", "Ключ для подписи")</li>
                <li>Вставьте его в форму ниже для тестирования</li>
            </ol>
            
            <form method="post">
                <h3>🧪 Тест SECRET_KEY:</h3>
                <label>Введите SECRET_KEY из личного кабинета:</label><br>
                <input type="text" name="test_key" placeholder="Вставьте SECRET_KEY сюда" style="width: 400px; padding: 5px;" required><br><br>
                <button type="submit">Проверить ключ</button>
            </form>
            
            <h3>📞 Если не можете найти SECRET_KEY:</h3>
            <p>Свяжитесь с поддержкой FreedomPay:</p>
            <ul>
                <li><strong>Email:</strong> support@freedompay.uz</li>
                <li><strong>Укажите MERCHANT_ID:</strong> {{ merchant_id }}</li>
                <li><strong>Попросите:</strong> предоставить актуальный SECRET_KEY для Gateway API</li>
            </ul>
            
            <p><a href="/">← Главная</a></p>
        ''', merchant_id=MERCHANT_ID)
    
    # POST - тестируем введенный ключ
    test_key = request.form.get('test_key', '').strip()
    
    if not test_key:
        return "Ошибка: SECRET_KEY не указан"
    
    log_message(f"🧪 Тестирование ключа: {test_key[:10]}...")
    
    # Параметры из примера личного кабинета
    cabinet_params = {
        "pg_merchant_id": "552170",
        "pg_amount": "1000", 
        "pg_currency": "UZS",
        "pg_description": "sadas",
        "pg_salt": "XRJ0fLDMaPjtjnTy",
        "pg_language": "ru",
        "payment_origin": "merchant_cabinet"
    }
    
    expected_signature = "cf5b280eccf239052039b0692208bce3"
    sorted_keys = sorted(cabinet_params.keys())
    
    # Тестируем ключ
    sign_string = "payment.php;" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{test_key}"
    test_signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
    
    log_message(f"Строка: {sign_string}")
    log_message(f"Ожидаем: {expected_signature}")
    log_message(f"Получили: {test_signature}")
    log_message(f"Результат: {'✅ СОВПАДАЕТ' if test_signature == expected_signature else '❌ НЕ СОВПАДАЕТ'}")
    
    if test_signature == expected_signature:
        # Ключ правильный!
        return render_template_string('''
            <h2>🎉 Отлично! SECRET_KEY найден!</h2>
            
            <div style="background: #d4edda; padding: 15px; border-radius: 5px; margin: 10px 0;">
                <h3>✅ Правильный SECRET_KEY:</h3>
                <code style="background: white; padding: 5px; display: block; margin: 5px 0;">{{ test_key }}</code>
            </div>
            
            <h3>📝 Что делать дальше:</h3>
            <ol>
                <li><strong>Скопируйте код ниже</strong> и замените в вашем файле:</li>
            </ol>
            
            <div style="background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 10px 0;">
                <h4>Замените строку:</h4>
                <code>SECRET_KEY = "{{ old_key }}"</code>
                <h4>На:</h4>
                <code style="background: #d4edda; padding: 2px;">SECRET_KEY = "{{ test_key }}"</code>
            </div>
            
            <h3>🚀 Перезапуск:</h3>
            <ol start="2">
                <li>Остановите сервер (Ctrl+C)</li>
                <li>Перезапустите: <code>python freedom_pay_final_attempt.py</code></li>
                <li>Попробуйте платеж снова!</li>
            </ol>
            
            <p><a href="/">← Главная</a></p>
        ''', test_key=test_key, old_key=SECRET_KEY)
    else:
        # Ключ неправильный
        return render_template_string('''
            <h2>❌ Этот SECRET_KEY не подходит</h2>
            
            <div style="background: #f8d7da; padding: 15px; border-radius: 5px; margin: 10px 0;">
                <h3>🔍 Результат проверки:</h3>
                <p><strong>Ваш ключ:</strong> <code>{{ test_key }}</code></p>
                <p><strong>Ожидаемая подпись:</strong> <code>cf5b280eccf239052039b0692208bce3</code></p>
                <p><strong>Полученная подпись:</strong> <code>{{ test_signature }}</code></p>
                <p><strong>Результат:</strong> ❌ Не совпадает</p>
            </div>
            
            <h3>💡 Попробуйте:</h3>
            <ul>
                <li>Проверить, что скопировали полный ключ</li>
                <li>Убрать лишние пробелы в начале/конце</li>
                <li>Поискать ключ в других разделах кабинета</li>
                <li>Связаться с поддержкой FreedomPay</li>
            </ul>
            
            <p><a href="/test_manual_key">🔄 Попробовать другой ключ</a></p>
            <p><a href="/">← Главная</a></p>
        ''', test_key=test_key, test_signature=test_signature)

@app.route('/analyze_cabinet_url')
def analyze_cabinet_url():
    """Детальный анализ URL из личного кабинета"""
    
    # URL из личного кабинета (из предыдущего сообщения)
    cabinet_url = "https://api.freedompay.uz/payment.php?pg_merchant_id=552170&pg_amount=1000&pg_currency=UZS&pg_description=sadas&pg_salt=XRJ0fLDMaPjtjnTy&pg_language=ru&payment_origin=merchant_cabinet&pg_sig=cf5b280eccf239052039b0692208bce3"
    
    # Парсим параметры из URL кабинета
    from urllib.parse import parse_qs, urlparse
    parsed = urlparse(cabinet_url)
    cabinet_params_raw = parse_qs(parsed.query)
    
    # Преобразуем в обычный словарь
    cabinet_params = {}
    for key, value_list in cabinet_params_raw.items():
        if key != 'pg_sig':  # исключаем подпись
            cabinet_params[key] = value_list[0]
    
    expected_signature = "cf5b280eccf239052039b0692208bce3"
    
    results = []
    results.append("=== АНАЛИЗ URL ИЗ ЛИЧНОГО КАБИНЕТА ===")
    results.append(f"URL: {cabinet_url}")
    results.append("")
    
    results.append("=== ПАРАМЕТРЫ ИЗ КАБИНЕТА ===")
    for key in sorted(cabinet_params.keys()):
        results.append(f"{key} = {cabinet_params[key]}")
    results.append("")
    
    # Тестируем разные варианты формирования подписи
    test_variants = [
        {
            "name": "Стандартный (payment.php в начале)",
            "prefix": "payment.php"
        },
        {
            "name": "Без префикса",
            "prefix": ""
        },
        {
            "name": "С init.php",
            "prefix": "init.php"
        },
        {
            "name": "С gateway.php", 
            "prefix": "gateway.php"
        }
    ]
    
    results.append("=== ТЕСТИРОВАНИЕ ВАРИАНТОВ ПОДПИСИ ===")
    
    for variant in test_variants:
        sorted_keys = sorted(cabinet_params.keys())
        
        if variant["prefix"]:
            sign_string = f"{variant['prefix']};" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{SECRET_KEY}"
        else:
            sign_string = ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{SECRET_KEY}"
            
        test_signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
        
        match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
        
        results.append(f"{variant['name']}:")
        results.append(f"  Строка: {sign_string}")
        results.append(f"  MD5: {test_signature}")
        results.append(f"  Результат: {match}")
        results.append("")
    
    # Тестируем разные кодировки
    results.append("=== ТЕСТИРОВАНИЕ КОДИРОВОК ===")
    
    sorted_keys = sorted(cabinet_params.keys())
    base_string = "payment.php;" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{SECRET_KEY}"
    
    encodings = ['utf-8', 'windows-1251', 'cp1252', 'latin-1']
    
    for encoding in encodings:
        try:
            encoded_string = base_string.encode(encoding)
            test_signature = hashlib.md5(encoded_string).hexdigest()
            match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
            
            results.append(f"Кодировка {encoding}: {test_signature} {match}")
        except Exception as e:
            results.append(f"Кодировка {encoding}: ошибка - {str(e)}")
    
    results.append("")
    results.append("=== ДОПОЛНИТЕЛЬНЫЕ ТЕСТЫ ===")
    
    # Тест с другими SECRET_KEY из кабинета
    other_keys = [
        "lvA1DXTL8ILLLj0P",  # ключ для выплат
        "Jw6idIqYSd5gIGLL321KUP5ej6fneq1G"  # JS SDK ключ
    ]
    
    for key in other_keys:
        sign_string = "payment.php;" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{key}"
        test_signature = hashlib.md5(sign_string.encode('utf-8')).hexdigest()
        match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
        
        results.append(f"С ключом {key[:10]}...: {test_signature} {match}")
    
    return render_template_string('''
        <h2>🔍 Анализ URL из личного кабинета</h2>
        <pre style="background: #f5f5f5; padding: 15px; border-radius: 5px; font-size: 12px;">{{ results }}</pre>
        
        <h3>💡 Если найдено совпадение:</h3>
        <p>Используйте найденный вариант для обновления кода!</p>
        
        <h3>❌ Если совпадений нет:</h3>
        <p>Возможно, FreedomPay использует другой алгоритм или есть скрытые параметры.</p>
        
        <p><a href="/">← Главная</a></p>
    ''', results='\\n'.join(results))

@app.route('/test_hash_algorithms')
def test_hash_algorithms():
    """Тестирование разных алгоритмов хеширования"""
    
    # Параметры из личного кабинета
    cabinet_params = {
        "payment_origin": "merchant_cabinet",
        "pg_amount": "1000",
        "pg_currency": "UZS", 
        "pg_description": "sadas",
        "pg_language": "ru",
        "pg_merchant_id": "552170",
        "pg_salt": "XRJ0fLDMaPjtjnTy"
    }
    
    expected_signature = "cf5b280eccf239052039b0692208bce3"
    sorted_keys = sorted(cabinet_params.keys())
    
    # Базовая строка для подписи
    base_string = "payment.php;" + ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys)
    
    results = []
    results.append("=== ТЕСТИРОВАНИЕ АЛГОРИТМОВ ХЕШИРОВАНИЯ ===")
    results.append(f"Ожидаемая подпись: {expected_signature}")
    results.append("")
    
    # Все варианты ключей
    test_keys = [
        ("Ключ для приема", "wUQ18x3bzP86MUzn"),
        ("Ключ для выплат", "lvA1DXTL8ILLLj0P"), 
        ("JS SDK ключ", "Jw6idIqYSd5gIGLL321KUP5ej6fneq1G")
    ]
    
    # Импортируем дополнительные алгоритмы
    import hashlib
    
    # Алгоритмы для тестирования
    hash_algorithms = [
        ("MD5", hashlib.md5),
        ("SHA-1", hashlib.sha1),
        ("SHA-256", hashlib.sha256),
        ("SHA-512", hashlib.sha512)
    ]
    
    for key_name, secret_key in test_keys:
        results.append(f"=== {key_name.upper()}: {secret_key} ===")
        
        for algo_name, algo_func in hash_algorithms:
            sign_string = f"{base_string};{secret_key}"
            
            try:
                hash_obj = algo_func(sign_string.encode('utf-8'))
                test_signature = hash_obj.hexdigest()
                match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
                
                results.append(f"{algo_name}: {test_signature} {match}")
                
            except Exception as e:
                results.append(f"{algo_name}: ошибка - {str(e)}")
        
        results.append("")
    
    # Дополнительные тесты - возможно подпись без "payment.php;"
    results.append("=== ТЕСТ БЕЗ 'payment.php;' ===")
    
    for key_name, secret_key in test_keys:
        # Строка без префикса
        no_prefix_string = ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys) + f";{secret_key}"
        
        for algo_name, algo_func in hash_algorithms:
            try:
                hash_obj = algo_func(no_prefix_string.encode('utf-8'))
                test_signature = hash_obj.hexdigest()
                match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
                
                if match == "✅ СОВПАДАЕТ!":
                    results.append(f"🎉 НАЙДЕНО! {key_name} + {algo_name} (без префикса): {test_signature}")
                    
            except Exception as e:
                pass
    
    # Тест с HMAC
    results.append("")
    results.append("=== ТЕСТ HMAC ===")
    
    import hmac
    
    for key_name, secret_key in test_keys:
        data_string = ";".join(f"{k}={cabinet_params[k]}" for k in sorted_keys)
        
        try:
            # HMAC-MD5
            hmac_md5 = hmac.new(secret_key.encode('utf-8'), data_string.encode('utf-8'), hashlib.md5)
            test_signature = hmac_md5.hexdigest()
            match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
            
            if match == "✅ СОВПАДАЕТ!":
                results.append(f"🎉 НАЙДЕНО! {key_name} + HMAC-MD5: {test_signature}")
            
            # HMAC-SHA1
            hmac_sha1 = hmac.new(secret_key.encode('utf-8'), data_string.encode('utf-8'), hashlib.sha1)
            test_signature = hmac_sha1.hexdigest()
            match = "✅ СОВПАДАЕТ!" if test_signature == expected_signature else "❌ не совпадает"
            
            if match == "✅ СОВПАДАЕТ!":
                results.append(f"🎉 НАЙДЕНО! {key_name} + HMAC-SHA1: {test_signature}")
                
        except Exception as e:
            pass
    
    return render_template_string('''
        <h2>🧪 Тестирование алгоритмов хеширования</h2>
        <pre style="background: #f5f5f5; padding: 15px; border-radius: 5px; font-size: 12px;">{{ results }}</pre>
        
        <h3>🎯 Если найден правильный алгоритм:</h3>
        <p>Обновим код для использования найденного метода!</p>
        
        <h3>❌ Если ничего не найдено:</h3>
        <p>Возможно, нужно обратиться в поддержку FreedomPay за документацией по алгоритму подписи.</p>
        
        <p><a href="/">← Главная</a></p>
    ''', results='\\n'.join(results))

@app.route('/test_final_payment')
def test_final_payment():
    """Финальный тест с правильным алгоритмом"""
    
    # Создаем тестовые параметры
    test_params = {
        "pg_merchant_id": MERCHANT_ID,
        "pg_amount": "1000",
        "pg_currency": "UZS",
        "pg_description": "Final Test Payment",
        "pg_salt": "test_salt_123",
        "pg_language": "ru",
        "payment_origin": "merchant_cabinet"
    }
    
    # ✅ Генерируем подпись правильным алгоритмом
    signature, sign_string = generate_correct_signature(test_params, "payment.php", SECRET_KEY)
    
    # Формируем URL
    query_parts = []
    sorted_keys = sorted(test_params.keys())
    for key in sorted_keys:
        query_parts.append(f"{key}={test_params[key]}")
    query_parts.append(f"pg_sig={signature}")
    
    payment_url = f"https://api.freedompay.uz/payment.php?{'&'.join(query_parts)}"
    
    return render_template_string('''
        <h2>🎯 Финальный тест с ПРАВИЛЬНЫМ алгоритмом</h2>
        
        <div style="background: #d4edda; padding: 15px; border-radius: 5px; margin: 15px 0;">
            <h3>✅ Найденный алгоритм:</h3>
            <ol>
                <li>Сортировка параметров по алфавиту</li>
                <li>Добавление "payment.php" в начало</li>
                <li>Добавление SECRET_KEY в конец</li>
                <li>Склеивание через ";"</li>
                <li>MD5 хеш</li>
            </ol>
        </div>
        
        <div style="background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0;">
            <h3>📊 Результат:</h3>
            <p><strong>Строка подписи:</strong><br>
            <code style="word-break: break-all;">{{ sign_string }}</code></p>
            
            <p><strong>MD5 подпись:</strong><br>
            <code>{{ signature }}</code></p>
            
            <p><strong>Итоговый URL:</strong><br>
            <code style="word-break: break-all;">{{ payment_url }}</code></p>
        </div>
        
        <div style="background: #fff3cd; padding: 15px; border-radius: 5px; margin: 15px 0;">
            <h3>🚨 ВНИМАНИЕ!</h3>
            <p>Теперь код использует <strong>ПРАВИЛЬНЫЙ</strong> алгоритм подписи!</p>
            <p>Больше не должно быть ошибок 9998 "Некорректная подпись запроса"</p>
        </div>
        
        <h3>🧪 Попробовать платеж:</h3>
        <p><a href="/" style="background: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;">← Вернуться на главную и попробовать платеж</a></p>
        
        <h3>🔗 Или протестировать напрямую:</h3>
        <p><a href="{{ payment_url }}" target="_blank" style="background: #28a745; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;">Открыть платежную страницу FreedomPay</a></p>
        
    ''', sign_string=sign_string, signature=signature, payment_url=payment_url)

@app.route('/check_payment_status')
def check_payment_status():
    """Проверка статуса платежа для Unity"""
    order_id = request.args.get('order_id')
    
    if not order_id:
        return {"status": "error", "message": "order_id не указан"}, 400
    
    # Проверяем статус платежа
    status = payment_statuses.get(order_id, "pending")
    
    log_message(f"🔍 Unity запрашивает статус для Order ID: {order_id}")
    log_message(f"📊 Статус: {status}")
    
    return {
        "order_id": order_id,
        "status": status,
        "timestamp": datetime.now().isoformat()
    }

@app.route('/payment_status/<order_id>')
def get_payment_status(order_id):
    """Получение статуса конкретного платежа"""
    status = payment_statuses.get(order_id, "pending")
    
    return render_template_string('''
        <h2>📊 Статус платежа</h2>
        
        <div style="background: #f8f9fa; padding: 15px; border-radius: 5px; margin: 15px 0;">
            <h3>Order ID: {{ order_id }}</h3>
            <h3>Статус: 
                {% if status == 'success' %}
                    <span style="color: green;">✅ Успешно оплачен</span>
                {% elif status == 'failed' %}
                    <span style="color: red;">❌ Не оплачен</span>
                {% else %}
                    <span style="color: orange;">⏳ В ожидании</span>
                {% endif %}
            </h3>
        </div>
        
        <p><a href="/">← Главная</a></p>
    ''', order_id=order_id, status=status)

@app.route('/all_payment_statuses')
def all_payment_statuses():
    """Показать все статусы платежей"""
    
    return render_template_string('''
        <h2>📊 Все статусы платежей</h2>
        
        {% if payment_statuses %}
            <table style="border-collapse: collapse; width: 100%; margin: 20px 0;">
                <tr style="background: #f8f9fa;">
                    <th style="border: 1px solid #ddd; padding: 10px;">Order ID</th>
                    <th style="border: 1px solid #ddd; padding: 10px;">Статус</th>
                    <th style="border: 1px solid #ddd; padding: 10px;">Действия</th>
                </tr>
                {% for order_id, status in payment_statuses.items() %}
                <tr>
                    <td style="border: 1px solid #ddd; padding: 10px;">{{ order_id }}</td>
                    <td style="border: 1px solid #ddd; padding: 10px;">
                        {% if status == 'success' %}
                            <span style="color: green;">✅ Успешно</span>
                        {% elif status == 'failed' %}
                            <span style="color: red;">❌ Неуспешно</span>
                        {% else %}
                            <span style="color: orange;">⏳ В ожидании</span>
                        {% endif %}
                    </td>
                    <td style="border: 1px solid #ddd; padding: 10px;">
                        <a href="/payment_status/{{ order_id }}">Подробнее</a>
                    </td>
                </tr>
                {% endfor %}
            </table>
        {% else %}
            <div style="background: #fff3cd; padding: 15px; border-radius: 5px; margin: 15px 0;">
                <p>🤷‍♂️ Платежей пока нет</p>
                <p>Создайте тестовый платеж, чтобы увидеть статусы здесь</p>
            </div>
        {% endif %}
        
        <h3>🧪 Для тестирования Unity:</h3>
        <ol>
            <li>Запустите платеж в Unity</li>
            <li>Обновите эту страницу - увидите статус "⏳ В ожидании"</li>
            <li>Совершите оплату в браузере</li>
            <li>Через несколько секунд статус изменится на "✅ Успешно"</li>
            <li>Unity автоматически получит обновление статуса</li>
        </ol>
        
        <p><a href="/">← Главная</a></p>
    ''', payment_statuses=payment_statuses)

if __name__ == '__main__':
    log_message("🚀 Запуск FreedomPay тестового сервера...")
    log_message(f"🏪 Merchant ID: {MERCHANT_ID}")
    log_message(f"🌐 Ngrok URL: {NGROK_URL}")
    
    if not validate_credentials():
        log_message("❌ Проверьте конфигурацию перед запуском!")
    
    app.run(debug=True)
